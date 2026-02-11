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
/// Writer sends via <see cref="ICodeEditorPresenter.PostWebMessage(string)"/>.
/// Reader feeds from <see cref="ICodeEditorPresenter.MessageReceived"/> into a
/// <see cref="Channel{T}"/> of UTF-8 byte sequences for deserialization.
/// </summary>
internal sealed class WebView2JsonRpcMessageHandler : IJsonRpcMessageHandler, IDisposable
{
    // 10 MB max payload size per bridge-protocol.md security constraints.
    private const int MaxPayloadSizeBytes = 10 * 1024 * 1024;

    // Known JS-to-C# methods (from bridge-protocol.md).
    private static readonly HashSet<string> KnownMethods = new(StringComparer.Ordinal)
    {
        "bridge/ready",
        "editor/ready",
        "parentAccessor/setValue",
        "parentAccessor/setValueWithType",
        "parentAccessor/callAction",
        "parentAccessor/callActionWithParameters",
        "parentAccessor/callEvent",
        "parentAccessor/getJsonValue",
        "debug/log",
        "keyboard/keyDown",
        "theme/getProperty",
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
        _inboundChannel = Channel.CreateUnbounded<ReadOnlySequence<byte>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
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
            _presenter.PostWebMessage(json);
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

        // Security: payload size limit (approximate -- UTF-16 length as proxy for byte size)
        if (json.Length > MaxPayloadSizeBytes)
        {
            Debug.WriteLine($"WebView2JsonRpcMessageHandler: Dropping message exceeding {MaxPayloadSizeBytes} byte limit (length={json.Length})");
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
                if (method is null || !KnownMethods.Contains(method))
                {
                    Debug.WriteLine($"WebView2JsonRpcMessageHandler: Dropping message with unknown method '{method}'");
                    return;
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
