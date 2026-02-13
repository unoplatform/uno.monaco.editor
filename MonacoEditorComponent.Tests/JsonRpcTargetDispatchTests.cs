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
        // Use NotifyWithParameterObjectAsync to send named params matching individual C# parameters.
        await _clientRpc!.NotifyWithParameterObjectAsync("parentAccessor/callAction", new { name = "testAction" });

        // Wait for async dispatch with deterministic signaling.
        Assert.True(
            await _target!.ActionCalledSignal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
            "callAction notification was not dispatched");
        Assert.Contains("testAction", _target.CalledActions);
    }

    [Fact]
    public async Task Request_ReturnsValue()
    {
        var result = await _clientRpc!.InvokeWithParameterObjectAsync<string>(
            "parentAccessor/getJsonValue",
            new { name = "TestProperty" });

        Assert.Equal("{\"TestProperty\":true}", result);
    }

    [Fact]
    public async Task Request_CallEvent_ReturnsResult()
    {
        using var doc = JsonDocument.Parse("[\"arg1\",\"arg2\"]");
        var result = await _clientRpc!.InvokeWithParameterObjectAsync<string>(
            "parentAccessor/callEvent",
            new { name = "testEvent", parameters = doc.RootElement });

        Assert.Equal("event-result", result);
    }

    [Fact]
    public async Task Notification_BridgeReady_Dispatches()
    {
        await _clientRpc!.NotifyWithParameterObjectAsync("bridge/ready", new { protocolVersion = 1 });

        // Wait for async dispatch with deterministic signaling.
        Assert.True(
            await _target!.BridgeReadySignal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
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
        public void OnCallAction(string name)
        {
            CalledActions.Add(name);
            ActionCalledSignal.TrySetResult(true);
        }

        [JsonRpcMethod("parentAccessor/getJsonValue")]
        public string OnGetJsonValue(string name)
        {
            return $"{{\"{name}\":true}}";
        }

        [JsonRpcMethod("parentAccessor/callEvent")]
        public string OnCallEvent(string name, JsonElement parameters)
        {
            return "event-result";
        }

        [JsonRpcMethod("bridge/ready")]
        public void OnBridgeReady(int protocolVersion)
        {
            BridgeReadyReceived = true;
            BridgeReadyProtocolVersion = protocolVersion;
            BridgeReadySignal.TrySetResult(true);
        }
    }

    /// <summary>
    /// Regression: StreamJsonRpc locks configuration after StartListening().
    /// AddLocalRpcTarget called after StartListening must throw.
    /// This documents the constraint that caused the WSL2 desktop init failure
    /// when SetupJsonRpc() called StartListening() before CreateBridgeTargets()
    /// registered the bridge helper targets.
    /// </summary>
    [Fact]
    public void AddLocalRpcTarget_AfterStartListening_Throws()
    {
        var pipes = FullDuplexStream.CreatePipePair();
        var formatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
        };

        using var rpc = new JsonRpc(new HeaderDelimitedMessageHandler(pipes.Item1, formatter));
        rpc.StartListening();

        Assert.Throws<InvalidOperationException>(() =>
            rpc.AddLocalRpcTarget(new MockBridgeTarget()));
    }

    /// <summary>
    /// Verifies the correct pattern: multiple targets registered before StartListening.
    /// This matches the production pattern in CreateBridgeTargets where handshake,
    /// parentAccessor, themeListener, keyboardListener, and debugLogger targets are
    /// all registered before the bridge starts listening.
    /// </summary>
    [Fact]
    public async Task MultipleTargets_RegisteredBeforeListening_AllDispatch()
    {
        var pipes = FullDuplexStream.CreatePipePair();
        var formatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
        };

        var target1 = new MockBridgeTarget();
        var target2 = new SecondaryTarget();

        using var serverRpc = new JsonRpc(new HeaderDelimitedMessageHandler(pipes.Item1, formatter));
        serverRpc.AddLocalRpcTarget(target1);
        serverRpc.AddLocalRpcTarget(target2);
        serverRpc.StartListening();

        using var clientRpc = new JsonRpc(new HeaderDelimitedMessageHandler(pipes.Item2, new SystemTextJsonFormatter
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
        }));
        clientRpc.StartListening();

        // Dispatch to first target (use named params for individual-parameter dispatch)
        await clientRpc.NotifyWithParameterObjectAsync("bridge/ready", new { protocolVersion = 1 });
        Assert.True(
            await target1.BridgeReadySignal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
            "bridge/ready not dispatched to first target");

        // Dispatch to second target (SecondaryTarget still uses DTO-style single param)
        await clientRpc.NotifyWithParameterObjectAsync("secondary/ping", new { value = "hello" });
        Assert.True(
            await target2.PingSignal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
            "secondary/ping not dispatched to second target");
        Assert.Equal("hello", target2.LastPingValue);
    }

    /// <summary>Secondary target for multi-target registration test.</summary>
    private sealed class SecondaryTarget
    {
        public string? LastPingValue { get; private set; }
        public TaskCompletionSource<bool> PingSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        [JsonRpcMethod("secondary/ping")]
        public void OnPing(string value)
        {
            LastPingValue = value;
            PingSignal.TrySetResult(true);
        }
    }

    /// <summary>
    /// Regression: StreamJsonRpc rejects targets with custom delegate events
    /// (only EventHandler/EventHandler&lt;T&gt; are supported). This documents
    /// why ThemeListenerDesktop.ThemeChanged uses EventHandler&lt;ThemeChangedEventArgs&gt;
    /// instead of a custom delegate.
    /// </summary>
    [Fact]
    public void AddLocalRpcTarget_CustomDelegateEvent_Throws()
    {
        var pipes = FullDuplexStream.CreatePipePair();
        var formatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
        };

        using var rpc = new JsonRpc(new HeaderDelimitedMessageHandler(pipes.Item1, formatter));

        // StreamJsonRpc rejects custom delegate events during target registration.
        Assert.ThrowsAny<Exception>(() =>
            rpc.AddLocalRpcTarget(new TargetWithCustomDelegateEvent()));
    }

    /// <summary>
    /// Verifies that targets with standard EventHandler&lt;T&gt; events register
    /// successfully. This is the pattern used by ThemeListenerDesktop after
    /// converting from custom ThemeChangedEvent to EventHandler&lt;ThemeChangedEventArgs&gt;.
    /// </summary>
    [Fact]
    public void AddLocalRpcTarget_StandardEventHandlerT_Succeeds()
    {
        var pipes = FullDuplexStream.CreatePipePair();
        var formatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
        };

        using var rpc = new JsonRpc(new HeaderDelimitedMessageHandler(pipes.Item1, formatter));

        // EventHandler<T> is supported — should not throw.
        rpc.AddLocalRpcTarget(new TargetWithStandardEvent());
    }

    /// <summary>Custom delegate that StreamJsonRpc rejects.</summary>
    private delegate void CustomEvent(string value);

    /// <summary>Target with a custom delegate event — fails AddLocalRpcTarget.</summary>
    private sealed class TargetWithCustomDelegateEvent
    {
        public event CustomEvent? Changed;

        [JsonRpcMethod("custom/ping")]
        public void OnPing() => Changed?.Invoke("ping");
    }

    /// <summary>EventArgs for standard event pattern test.</summary>
    private sealed class StandardEventArgs : EventArgs;

    /// <summary>Target with EventHandler&lt;T&gt; event — succeeds AddLocalRpcTarget.</summary>
    private sealed class TargetWithStandardEvent
    {
        public event EventHandler<StandardEventArgs>? Changed;

        [JsonRpcMethod("standard/ping")]
        public void OnPing() => Changed?.Invoke(this, new StandardEventArgs());
    }

    /// <summary>
    /// Validates that all desktop bridge target types used in CreateBridgeTargets
    /// have events compatible with StreamJsonRpc (EventHandler or EventHandler&lt;T&gt; only).
    /// Catches delegate incompatibilities at unit test time instead of at runtime.
    /// </summary>
    [Theory]
    [InlineData(typeof(Monaco.Helpers.ThemeListenerDesktop))]
    [InlineData(typeof(Monaco.Helpers.ParentAccessorDesktop))]
    [InlineData(typeof(Monaco.Helpers.KeyboardListenerDesktop))]
    [InlineData(typeof(Monaco.Helpers.DebugLoggerDesktop))]
    public void BridgeTargetType_EventDelegates_AreStreamJsonRpcCompatible(Type targetType)
    {
        var events = targetType.GetEvents(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        foreach (var evt in events)
        {
            var handlerType = evt.EventHandlerType!;

            // StreamJsonRpc accepts: EventHandler, or EventHandler<T> where T : EventArgs
            var isEventHandler = handlerType == typeof(EventHandler);
            var isGenericEventHandler = handlerType.IsGenericType
                && handlerType.GetGenericTypeDefinition() == typeof(EventHandler<>)
                && typeof(EventArgs).IsAssignableFrom(handlerType.GetGenericArguments()[0]);

            Assert.True(
                isEventHandler || isGenericEventHandler,
                $"{targetType.Name}.{evt.Name} uses delegate {handlerType.Name} which is not " +
                $"compatible with StreamJsonRpc. Use EventHandler or EventHandler<T> where T : EventArgs.");
        }
    }

}
