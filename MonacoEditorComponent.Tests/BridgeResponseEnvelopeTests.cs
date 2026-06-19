using System.Text.Json;

using Monaco;
using Monaco.Bridge;

using StreamJsonRpc;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Verifies the exact JSON-RPC <b>response</b> envelope that C# posts back to JS when a
/// <c>parentAccessor/getJsonValue</c> request is dispatched through the production
/// <see cref="WebView2JsonRpcMessageHandler"/> + <see cref="SystemTextJsonFormatter"/>
/// pipeline. The envelope must be a well-formed JSON-RPC 2.0 response that vscode-jsonrpc
/// (the JS client) can correlate: <c>{"jsonrpc":"2.0","id":&lt;n&gt;,"result":&lt;value&gt;}</c>.
/// Complements the inbound/notification coverage in <see cref="JsonRpcWireCompatibilityTests"/>,
/// which did not exercise the outbound request→response path.
/// </summary>
public sealed class BridgeResponseEnvelopeTests
{
    private sealed class Target
    {
        [JsonRpcMethod("parentAccessor/getJsonValue")]
        public Task<string> OnGetJsonValue(string name) => Task.FromResult($"\"{name}-value\"");
    }

    [Fact]
    public async Task GetJsonValue_ResponseEnvelope_IsCorrelatable()
    {
        var presenter = new MockCodeEditorPresenter();
        var formatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                TypeInfoResolverChain = { BridgeSerializerContext.Default },
            },
        };

        using var handler = new WebView2JsonRpcMessageHandler(presenter, formatter);
        using var rpc = new JsonRpc(handler);
        rpc.AddLocalRpcTarget(new Target());
        rpc.StartListening();

        // vscode-jsonrpc sends a single object param by-name with a numeric id starting at 0.
        presenter.SimulateMessageReceived(
            """{"jsonrpc":"2.0","id":0,"method":"parentAccessor/getJsonValue","params":{"name":"Text"}}""");

        // Wait for the response to be posted back.
        for (int i = 0; i < 100 && presenter.PostedMessages.Count == 0; i++)
        {
            await Task.Delay(20);
        }

        Assert.NotEmpty(presenter.PostedMessages);
        var envelope = presenter.PostedMessages[^1];

        using var doc = JsonDocument.Parse(envelope);
        var root = doc.RootElement;

        // jsonrpc version marker.
        Assert.True(root.TryGetProperty("jsonrpc", out var version));
        Assert.Equal("2.0", version.GetString());

        // id must echo the request id (0) as a number so vscode-jsonrpc correlates it.
        Assert.True(root.TryGetProperty("id", out var id));
        Assert.Equal(JsonValueKind.Number, id.ValueKind);
        Assert.Equal(0, id.GetInt32());

        // result carries the JSON-encoded property value.
        Assert.True(root.TryGetProperty("result", out var result));
        Assert.Equal("\"Text-value\"", result.GetString());

        // A response has no method/params; only the response fields are present.
        var fields = root.EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.True(fields.IsSubsetOf(new HashSet<string> { "jsonrpc", "id", "result" }),
            $"Unexpected response fields: {string.Join(",", fields)}");
    }
}
