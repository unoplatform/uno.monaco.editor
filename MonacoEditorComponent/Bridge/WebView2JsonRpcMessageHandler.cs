using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace Monaco.Bridge;

/// <summary>
/// Custom <see cref="IJsonRpcMessageHandler"/> that bridges StreamJsonRpc
/// with WebView2's postMessage/WebMessageReceived transport.
/// Writer sends via <see cref="ICodeEditorPresenter.PostWebMessageAsync(string)"/>.
/// Reader feeds from <see cref="ICodeEditorPresenter.MessageReceived"/> into a
/// <see cref="Channel{T}"/> of UTF-8 byte sequences for deserialization.
/// </summary>
/// <remarks>
/// <para>Security controls:</para>
/// <list type="bullet">
/// <item><description>Method allowlist — only explicitly registered RPC target methods are dispatchable;
/// unrecognized methods are rejected by StreamJsonRpc before reaching application code.</description></item>
/// <item><description>Payload size cap — inbound messages exceeding <c>10 MB</c> are dropped to prevent
/// memory exhaustion from a hostile or buggy web side.</description></item>
/// <item><description>Bounded inbound queue — the <see cref="Channel{T}"/> is capped at 256 messages,
/// applying back-pressure and preventing unbounded memory growth.</description></item>
/// <item><description>Parameter validation — each dispatchable method declares its required parameter
/// names; payloads missing required fields are rejected before dispatch.</description></item>
/// </list>
/// </remarks>
internal sealed class WebView2JsonRpcMessageHandler : IJsonRpcMessageHandler, IDisposable
{
    // 10 MB max payload size per bridge-protocol.md security constraints.
    private const int MaxPayloadSizeBytes = 10 * 1024 * 1024;

    // Max queued inbound messages. Prevents unbounded memory growth from
    // a hostile or buggy web side sending faster than we can dispatch.
    private const int MaxQueuedMessages = 256;

    // Per-method required params validation. Each entry maps a method name
    // to the set of required param field names (must be present as object properties).
    // Methods with no required params map to an empty array.
    private static readonly Dictionary<string, string[]> MethodParamRequirements = new(StringComparer.Ordinal)
    {
        ["bridge/ready"] = ["protocolVersion"],
        ["editor/ready"] = ["protocolVersion"],
        ["parentAccessor/setValue"] = ["name", "value"],
        ["parentAccessor/setValueWithType"] = ["name", "value", "typeName"],
        ["parentAccessor/callAction"] = ["name"],
        ["parentAccessor/callActionWithParameters"] = ["name", "parameters"],
        ["parentAccessor/callEvent"] = ["name", "parameters"],
        ["parentAccessor/getJsonValue"] = ["name"],
        ["debug/log"] = ["level", "message"],
        ["keyboard/keyDown"] = ["keyCode", "ctrlKey", "shiftKey", "altKey", "metaKey"],
        ["theme/getProperty"] = ["name"],
        // StreamJsonRpc cancellation support (native JSON-RPC protocol extension).
        ["$/cancelRequest"] = ["id"],
    };

    private readonly ICodeEditorPresenter _presenter;
    private readonly SystemTextJsonFormatter _formatter;
    private readonly Channel<ReadOnlySequence<byte>> _inboundChannel;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    public WebView2JsonRpcMessageHandler(ICodeEditorPresenter presenter, SystemTextJsonFormatter formatter)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(formatter);

        _presenter = presenter;
        _formatter = formatter;
        _inboundChannel = Channel.CreateBounded<ReadOnlySequence<byte>>(new BoundedChannelOptions(MaxQueuedMessages)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        // Subscribe to inbound messages from the presenter.
        _presenter.MessageReceived += OnMessageReceived;
    }

    public IJsonRpcMessageFormatter Formatter => _formatter;

    public bool CanRead => true;
    public bool CanWrite => true;

    public async ValueTask<JsonRpcMessage?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _inboundChannel.Reader.WaitToReadAsync(cancellationToken))
            {
                if (_inboundChannel.Reader.TryRead(out var bytes))
                {
                    return _formatter.Deserialize(bytes);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (ChannelClosedException)
        {
            // Channel completed -- transport shutting down
        }

        return null;
    }

    public async ValueTask WriteAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var writer = new ArrayBufferWriter<byte>();
            _formatter.Serialize(writer, message);
            var json = Encoding.UTF8.GetString(writer.WrittenSpan);
            await _presenter.PostWebMessageAsync(json);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void OnMessageReceived(object? sender, WebViewMessageEventArgs e)
    {
        if (_disposed) return;

        var json = e.MessageJson;
        if (string.IsNullOrEmpty(json)) return;

        // Security: payload size limit using actual UTF-8 byte count.
        var byteCount = Encoding.UTF8.GetByteCount(json);
        if (byteCount > MaxPayloadSizeBytes)
        {
            Debug.WriteLine($"WebView2JsonRpcMessageHandler: Dropping message exceeding {MaxPayloadSizeBytes} byte limit (size={byteCount})");
            return;
        }

        try
        {
            // Parse the raw JSON to inspect envelope type for security validation.
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Determine envelope type by presence of "method" field (request/notification)
            // vs "id" + ("result" or "error") (response).
            if (root.TryGetProperty("method", out var methodElement))
            {
                // Request or notification: validate method is in the allowlist.
                var method = methodElement.GetString();
                if (method is null || !MethodParamRequirements.TryGetValue(method, out var requiredParams))
                {
                    Debug.WriteLine($"WebView2JsonRpcMessageHandler: Dropping message with unknown method '{method}'");
                    return;
                }

                // Validate required params are present.
                if (requiredParams.Length > 0)
                {
                    if (!root.TryGetProperty("params", out var paramsElement) ||
                        paramsElement.ValueKind != JsonValueKind.Object)
                    {
                        Debug.WriteLine($"WebView2JsonRpcMessageHandler: Dropping '{method}' -- missing or non-object params");
                        return;
                    }

                    foreach (var required in requiredParams)
                    {
                        if (!paramsElement.TryGetProperty(required, out _))
                        {
                            Debug.WriteLine($"WebView2JsonRpcMessageHandler: Dropping '{method}' -- missing required param '{required}'");
                            return;
                        }
                    }
                }
            }
            else if (root.TryGetProperty("id", out _))
            {
                // Response: must have id field. StreamJsonRpc handles correlation
                // (unknown IDs are safely ignored by the library).
                // Just validate basic structure -- must have result or error.
                if (!root.TryGetProperty("result", out _) && !root.TryGetProperty("error", out _))
                {
                    Debug.WriteLine("WebView2JsonRpcMessageHandler: Dropping response without result or error field");
                    return;
                }
            }
            else
            {
                // Neither request/notification nor response -- drop.
                Debug.WriteLine("WebView2JsonRpcMessageHandler: Dropping unrecognized message envelope");
                return;
            }

            // Convert string to UTF-8 bytes for the formatter.
            var bytes = Encoding.UTF8.GetBytes(json);
            _inboundChannel.Writer.TryWrite(new ReadOnlySequence<byte>(bytes));
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"WebView2JsonRpcMessageHandler: Failed to parse inbound message: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView2JsonRpcMessageHandler: Error processing inbound message: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _presenter.MessageReceived -= OnMessageReceived;
        _inboundChannel.Writer.TryComplete();
        _writeLock.Dispose();
    }
}
