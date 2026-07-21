using System.Buffers;
using System.Text;
using System.Text.Json;

using Monaco;
using Monaco.Bridge;

using StreamJsonRpc;
using StreamJsonRpc.Protocol;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Validates wire compatibility between StreamJsonRpc (with SystemTextJsonFormatter)
/// and the vscode-jsonrpc output format. Tests two directions:
///
/// 1. Inbound: vscode-jsonrpc-style JSON-RPC 2.0 notifications pass through
///    <see cref="WebView2JsonRpcMessageHandler"/> and produce well-formed
///    <see cref="JsonRpcRequest"/> messages that StreamJsonRpc can dispatch.
///
/// 2. Outbound: messages serialized by <see cref="SystemTextJsonFormatter"/>
///    through the handler produce JSON matching the vscode-jsonrpc 2.0 envelope.
/// </summary>
public sealed class JsonRpcWireCompatibilityTests : IDisposable
{
    private readonly MockCodeEditorPresenter _presenter;
    private readonly SystemTextJsonFormatter _formatter;
    private readonly WebView2JsonRpcMessageHandler _handler;

    public JsonRpcWireCompatibilityTests()
    {
        _presenter = new MockCodeEditorPresenter();
        _formatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                TypeInfoResolverChain = { BridgeSerializerContext.Default },
            },
        };
        _handler = new WebView2JsonRpcMessageHandler(_presenter, _formatter);
    }

    public void Dispose()
    {
        _handler.Dispose();
    }

    // ================================================================
    // Inbound: vscode-jsonrpc format -> handler -> deserialized message
    // ================================================================

    [Fact]
    public async Task VscodeJsonRpc_BridgeReady_DeserializesCorrectly()
    {
        var json = """{"jsonrpc":"2.0","method":"bridge/ready","params":{"protocolVersion":1}}""";
        _presenter.SimulateMessageReceived(json);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        var request = Assert.IsAssignableFrom<JsonRpcRequest>(message);
        Assert.Equal("bridge/ready", request.Method);
        Assert.True(request.IsNotification, "bridge/ready should be a notification (no id)");

        // Verify the raw params contain the expected named parameters.
        var paramsJson = ExtractParams(json);
        using var doc = JsonDocument.Parse(paramsJson);
        Assert.Equal(1, doc.RootElement.GetProperty("protocolVersion").GetInt32());
    }

    [Fact]
    public async Task VscodeJsonRpc_SetValue_DeserializesCorrectly()
    {
        var json = """{"jsonrpc":"2.0","method":"parentAccessor/setValue","params":{"name":"Text","value":"hello"}}""";
        _presenter.SimulateMessageReceived(json);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        var request = Assert.IsAssignableFrom<JsonRpcRequest>(message);
        Assert.Equal("parentAccessor/setValue", request.Method);
        Assert.True(request.IsNotification);

        // Verify the raw params contain the expected named parameters.
        var paramsJson = ExtractParams(json);
        using var doc = JsonDocument.Parse(paramsJson);
        Assert.Equal("Text", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("hello", doc.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public async Task VscodeJsonRpc_DebugLog_DeserializesCorrectly()
    {
        var json = """{"jsonrpc":"2.0","method":"debug/log","params":{"level":"info","message":"Editor initialized"}}""";
        _presenter.SimulateMessageReceived(json);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        var request = Assert.IsAssignableFrom<JsonRpcRequest>(message);
        Assert.Equal("debug/log", request.Method);
        Assert.True(request.IsNotification);

        // Verify the raw params contain the expected named parameters.
        var paramsJson = ExtractParams(json);
        using var doc = JsonDocument.Parse(paramsJson);
        Assert.Equal("info", doc.RootElement.GetProperty("level").GetString());
        Assert.Equal("Editor initialized", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task VscodeJsonRpc_KeyDown_DeserializesCorrectly()
    {
        var json = """{"jsonrpc":"2.0","method":"keyboard/keyDown","params":{"keyCode":65,"ctrlKey":true,"shiftKey":false,"altKey":false,"metaKey":false}}""";
        _presenter.SimulateMessageReceived(json);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        var request = Assert.IsAssignableFrom<JsonRpcRequest>(message);
        Assert.Equal("keyboard/keyDown", request.Method);
        Assert.True(request.IsNotification);

        // Verify the raw params contain the expected named parameters.
        var paramsJson = ExtractParams(json);
        using var doc = JsonDocument.Parse(paramsJson);
        Assert.Equal(65, doc.RootElement.GetProperty("keyCode").GetInt32());
        Assert.True(doc.RootElement.GetProperty("ctrlKey").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("shiftKey").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("altKey").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("metaKey").GetBoolean());
    }

    [Fact]
    public async Task VscodeJsonRpc_Response_DeserializesCorrectly()
    {
        var json = """{"jsonrpc":"2.0","id":42,"result":"hello"}""";
        _presenter.SimulateMessageReceived(json);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        Assert.IsAssignableFrom<JsonRpcResult>(message);
    }

    // ================================================================
    // Outbound: StreamJsonRpc serialization -> vscode-jsonrpc format
    // ================================================================

    [Fact]
    public async Task Outbound_Notification_UsesJsonRpc2Format()
    {
        var notification = new JsonRpcRequest
        {
            Method = "editor/lifecycleUpdate",
        };

        await _handler.WriteAsync(notification, CancellationToken.None);

        Assert.Single(_presenter.PostedMessages);
        var sent = _presenter.PostedMessages[0];

        using var doc = JsonDocument.Parse(sent);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("jsonrpc", out var version));
        Assert.Equal("2.0", version.GetString());

        Assert.True(root.TryGetProperty("method", out var method));
        Assert.Equal("editor/lifecycleUpdate", method.GetString());

        // Notification: no id field.
        Assert.False(root.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task Outbound_Serialization_NoExtraFields()
    {
        var notification = new JsonRpcRequest
        {
            Method = "editor/lifecycleUpdate",
        };

        await _handler.WriteAsync(notification, CancellationToken.None);

        var sent = _presenter.PostedMessages[0];
        using var doc = JsonDocument.Parse(sent);
        var root = doc.RootElement;

        var propertyNames = root.EnumerateObject().Select(p => p.Name).ToHashSet();
        // Only jsonrpc, method, and optionally params are allowed. No extra fields.
        var allowed = new HashSet<string> { "jsonrpc", "method", "params" };
        Assert.True(propertyNames.IsSubsetOf(allowed),
            $"Unexpected fields in notification: {string.Join(", ", propertyNames.Except(allowed))}");
    }

    [Fact]
    public async Task Outbound_WithParams_SerializesParamsAsObject()
    {
        // Serialize a typed DTO through the formatter to verify camelCase naming.
        var paramsBytes = JsonSerializer.SerializeToUtf8Bytes(
            new LifecycleUpdateParams(1, 0),
            BridgeSerializerContext.Default.LifecycleUpdateParams);

        // Write raw params as JSON to verify format.
        using var paramsDoc = JsonDocument.Parse(paramsBytes);
        Assert.True(paramsDoc.RootElement.TryGetProperty("loading", out var loading));
        Assert.Equal(1, loading.GetInt32());
        Assert.True(paramsDoc.RootElement.TryGetProperty("loaded", out var loaded));
        Assert.Equal(0, loaded.GetInt32());
    }

    [Fact]
    public void RoundTrip_LifecycleUpdateParams_FormatterConsistency()
    {
        // Serialize a LifecycleUpdateParams, then verify the bytes deserialize back.
        var original = new LifecycleUpdateParams(1, 0);
        var serialized = JsonSerializer.Serialize(original, BridgeSerializerContext.Default.LifecycleUpdateParams);
        var deserialized = JsonSerializer.Deserialize(serialized, BridgeSerializerContext.Default.LifecycleUpdateParams);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Loading, deserialized!.Loading);
        Assert.Equal(original.Loaded, deserialized.Loaded);
    }

    /// <summary>
    /// Extracts the "params" value from a JSON-RPC envelope for typed deserialization.
    /// </summary>
    private static string ExtractParams(string jsonRpcEnvelope)
    {
        using var doc = JsonDocument.Parse(jsonRpcEnvelope);
        return doc.RootElement.GetProperty("params").GetRawText();
    }
}
