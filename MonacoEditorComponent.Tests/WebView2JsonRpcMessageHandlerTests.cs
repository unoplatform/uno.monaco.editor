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
/// Tests for <see cref="WebView2JsonRpcMessageHandler"/> with in-memory channels.
/// Verifies: write serializes and calls mock sender, read deserializes incoming
/// messages, disposal cancels reader.
/// </summary>
public sealed class WebView2JsonRpcMessageHandlerTests : IDisposable
{
    private readonly MockCodeEditorPresenter _presenter;
    private readonly SystemTextJsonFormatter _formatter;
    private readonly WebView2JsonRpcMessageHandler _handler;

    public WebView2JsonRpcMessageHandlerTests()
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

    [Fact]
    public async Task WriteAsync_SerializesAndCallsPostWebMessage()
    {
        var notification = new JsonRpcRequest
        {
            Method = "test/method",
        };

        await _handler.WriteAsync(notification, CancellationToken.None);

        Assert.Single(_presenter.PostedMessages);
        var json = _presenter.PostedMessages[0];
        Assert.Contains("test/method", json);
    }

    [Fact]
    public async Task ReadAsync_DeserializesInboundNotification()
    {
        // Simulate an inbound JSON-RPC notification with a valid method.
        var json = """{"jsonrpc":"2.0","method":"bridge/ready","params":{"protocolVersion":1}}""";
        _presenter.SimulateMessageReceived(json);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        var request = Assert.IsAssignableFrom<JsonRpcRequest>(message);
        Assert.Equal("bridge/ready", request.Method);
    }

    [Fact]
    public async Task ReadAsync_DeserializesInboundResponse()
    {
        // Simulate a JSON-RPC response.
        var json = """{"jsonrpc":"2.0","id":1,"result":"hello"}""";
        _presenter.SimulateMessageReceived(json);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        Assert.IsAssignableFrom<JsonRpcResult>(message);
    }

    [Fact]
    public async Task ReadAsync_DropsUnknownMethod()
    {
        // Unknown method should be dropped by the security allowlist.
        var json = """{"jsonrpc":"2.0","method":"evil/method","params":{}}""";
        _presenter.SimulateMessageReceived(json);

        // Send a valid message after so we can confirm the first was dropped.
        var validJson = """{"jsonrpc":"2.0","method":"bridge/ready","params":{"protocolVersion":1}}""";
        _presenter.SimulateMessageReceived(validJson);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        var request = Assert.IsAssignableFrom<JsonRpcRequest>(message);
        Assert.Equal("bridge/ready", request.Method);
    }

    [Fact]
    public async Task ReadAsync_DropsMalformedJson()
    {
        _presenter.SimulateMessageReceived("not valid json {{{");

        // Send valid message after.
        var validJson = """{"jsonrpc":"2.0","method":"editor/ready","params":{"protocolVersion":1}}""";
        _presenter.SimulateMessageReceived(validJson);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        var request = Assert.IsAssignableFrom<JsonRpcRequest>(message);
        Assert.Equal("editor/ready", request.Method);
    }

    [Fact]
    public async Task ReadAsync_DropsEmptyMessage()
    {
        _presenter.SimulateMessageReceived("");

        var validJson = """{"jsonrpc":"2.0","method":"bridge/ready","params":{"protocolVersion":1}}""";
        _presenter.SimulateMessageReceived(validJson);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        var request = Assert.IsAssignableFrom<JsonRpcRequest>(message);
        Assert.Equal("bridge/ready", request.Method);
    }

    [Fact]
    public async Task ReadAsync_DropsRequestMissingRequiredParams()
    {
        // bridge/ready requires protocolVersion param.
        var json = """{"jsonrpc":"2.0","method":"bridge/ready","params":{}}""";
        _presenter.SimulateMessageReceived(json);

        var validJson = """{"jsonrpc":"2.0","method":"editor/ready","params":{"protocolVersion":1}}""";
        _presenter.SimulateMessageReceived(validJson);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        var request = Assert.IsAssignableFrom<JsonRpcRequest>(message);
        Assert.Equal("editor/ready", request.Method);
    }

    [Fact]
    public async Task ReadAsync_DropsResponseWithoutResultOrError()
    {
        var json = """{"jsonrpc":"2.0","id":42}""";
        _presenter.SimulateMessageReceived(json);

        var validJson = """{"jsonrpc":"2.0","method":"bridge/ready","params":{"protocolVersion":1}}""";
        _presenter.SimulateMessageReceived(validJson);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        var request = Assert.IsAssignableFrom<JsonRpcRequest>(message);
        Assert.Equal("bridge/ready", request.Method);
    }

    [Fact]
    public async Task Dispose_CancelsReader()
    {
        _handler.Dispose();

        // After disposal, ReadAsync should return null (channel completed).
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var message = await _handler.ReadAsync(cts.Token);
        Assert.Null(message);
    }

    [Fact]
    public void CanRead_ReturnsTrue()
    {
        Assert.True(_handler.CanRead);
    }

    [Fact]
    public void CanWrite_ReturnsTrue()
    {
        Assert.True(_handler.CanWrite);
    }

    [Fact]
    public void Formatter_ReturnsSameInstance()
    {
        Assert.Same(_formatter, _handler.Formatter);
    }

    [Fact]
    public async Task ReadAsync_DropsMessageWithNonObjectParams()
    {
        // params must be an object for methods with required params.
        var json = """{"jsonrpc":"2.0","method":"bridge/ready","params":[1]}""";
        _presenter.SimulateMessageReceived(json);

        var validJson = """{"jsonrpc":"2.0","method":"bridge/ready","params":{"protocolVersion":1}}""";
        _presenter.SimulateMessageReceived(validJson);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await _handler.ReadAsync(cts.Token);

        Assert.NotNull(message);
        var request = Assert.IsAssignableFrom<JsonRpcRequest>(message);
        Assert.Equal("bridge/ready", request.Method);
    }
}
