using System.IO.Pipelines;
using System.Text.Json;

using Nerdbank.Streams;

using StreamJsonRpc;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Tests JSON-RPC target dispatch via in-process pipe.
/// Constructs JsonRpc over Nerdbank.FullDuplexStream, attaches a mock bridge target
/// with [JsonRpcMethod] attributes, sends JSON-RPC messages, and verifies method
/// dispatch and return values.
/// </summary>
public sealed class JsonRpcTargetDispatchTests : IAsyncLifetime
{
    private (IDuplexPipe, IDuplexPipe) _pipes;
    private JsonRpc? _clientRpc;
    private JsonRpc? _serverRpc;
    private MockBridgeTarget? _target;

    public ValueTask InitializeAsync()
    {
        _pipes = FullDuplexStream.CreatePipePair();

        _target = new MockBridgeTarget();

        var serverFormatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
        };
        var clientFormatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
        };

        _serverRpc = new JsonRpc(new HeaderDelimitedMessageHandler(_pipes.Item1, serverFormatter));
        _serverRpc.AddLocalRpcTarget(_target);
        _serverRpc.StartListening();

        _clientRpc = new JsonRpc(new HeaderDelimitedMessageHandler(_pipes.Item2, clientFormatter));
        _clientRpc.StartListening();

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _clientRpc?.Dispose();
        _serverRpc?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Notification_DispatchesToTarget()
    {
        await _clientRpc!.NotifyAsync("parentAccessor/callAction", new { name = "testAction" });

        // Wait for async dispatch with deterministic signaling.
        Assert.True(
            await _target!.ActionCalledSignal.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            "callAction notification was not dispatched");
        Assert.Contains("testAction", _target.CalledActions);
    }

    [Fact]
    public async Task Request_ReturnsValue()
    {
        var result = await _clientRpc!.InvokeAsync<string>(
            "parentAccessor/getJsonValue",
            new { name = "TestProperty" });

        Assert.Equal("{\"TestProperty\":true}", result);
    }

    [Fact]
    public async Task Request_CallEvent_ReturnsResult()
    {
        using var doc = JsonDocument.Parse("[\"arg1\",\"arg2\"]");
        var result = await _clientRpc!.InvokeAsync<string>(
            "parentAccessor/callEvent",
            new { name = "testEvent", parameters = doc.RootElement });

        Assert.Equal("event-result", result);
    }

    [Fact]
    public async Task Notification_BridgeReady_Dispatches()
    {
        await _clientRpc!.NotifyAsync("bridge/ready", new { protocolVersion = 1 });

        // Wait for async dispatch with deterministic signaling.
        Assert.True(
            await _target!.BridgeReadySignal.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            "bridge/ready notification was not dispatched");
        Assert.True(_target.BridgeReadyReceived);
        Assert.Equal(1, _target.BridgeReadyProtocolVersion);
    }

    /// <summary>
    /// Mock bridge target with [JsonRpcMethod] attributes for testing dispatch.
    /// Uses TaskCompletionSource for deterministic signaling instead of Task.Delay.
    /// </summary>
    private sealed class MockBridgeTarget
    {
        public List<string> CalledActions { get; } = [];
        public bool BridgeReadyReceived { get; private set; }
        public int BridgeReadyProtocolVersion { get; private set; }

        /// <summary>Signal set when callAction is dispatched.</summary>
        public TaskCompletionSource<bool> ActionCalledSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Signal set when bridge/ready is dispatched.</summary>
        public TaskCompletionSource<bool> BridgeReadySignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        [JsonRpcMethod("parentAccessor/callAction")]
        public void OnCallAction(CallActionDto p)
        {
            CalledActions.Add(p.Name);
            ActionCalledSignal.TrySetResult(true);
        }

        [JsonRpcMethod("parentAccessor/getJsonValue")]
        public string OnGetJsonValue(GetJsonValueDto p)
        {
            return $"{{\"{p.Name}\":true}}";
        }

        [JsonRpcMethod("parentAccessor/callEvent")]
        public string OnCallEvent(CallEventDto p)
        {
            return "event-result";
        }

        [JsonRpcMethod("bridge/ready")]
        public void OnBridgeReady(BridgeReadyDto p)
        {
            BridgeReadyReceived = true;
            BridgeReadyProtocolVersion = p.ProtocolVersion;
            BridgeReadySignal.TrySetResult(true);
        }
    }

    // DTOs used by the mock target (match bridge-protocol.md schemas).
    private record CallActionDto(string Name);
    private record GetJsonValueDto(string Name);
    private record CallEventDto(string Name, JsonElement Parameters);
    private record BridgeReadyDto(int ProtocolVersion);
}
