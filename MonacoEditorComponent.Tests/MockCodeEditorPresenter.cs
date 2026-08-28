using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

using Monaco;
using Monaco.Helpers;

using Windows.Foundation;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Minimal mock of <see cref="ICodeEditorPresenter"/> for testing
/// <see cref="Monaco.Bridge.WebView2JsonRpcMessageHandler"/> without
/// requiring a real WebView2 or XAML visual tree.
/// </summary>
internal sealed class MockCodeEditorPresenter : ICodeEditorPresenter
{
    public List<string> PostedMessages { get; } = [];

    /// <summary>Records each (method, args) pair passed to <see cref="InvokeMethodAsync"/>.</summary>
    public List<(string Method, string[] Args)> InvokeMethodCalls { get; } = [];

    /// <summary>Records each script passed to <see cref="InvokeScriptWithElementAsync"/>.</summary>
    public List<string> InvokeScriptWithElementCalls { get; } = [];

    /// <summary>Records each script passed to <see cref="InvokeScriptAsync"/>.</summary>
    public List<string> InvokeScriptCalls { get; } = [];

    public event TypedEventHandler<ICodeEditorPresenter?, PresenterNewWindowRequestedEventArgs?>? NewWindowRequested;
    public event TypedEventHandler<ICodeEditorPresenter?, PresenterNavigationStartingEventArgs?>? NavigationStarting;
    public event TypedEventHandler<ICodeEditorPresenter?, PresenterNavigationCompletedEventArgs?>? NavigationCompleted;
    public event EventHandler<WebViewMessageEventArgs>? MessageReceived;
    public event RoutedEventHandler? Loaded;

    public EditorHostBase? ParentCodeEditor { get; set; }
    public global::System.Uri Source { get; set; } = new global::System.Uri("about:blank");
    public DispatcherQueue DispatcherQueue => DispatcherQueue.GetForCurrentThread()!;
    public string ElementId => "mock-presenter";
    public bool IsSettingValue { get; set; }
    public bool IsLoaded => true;

    public bool TriggerKeyDown(WebKeyEventArgs args) => false;
    public bool Focus(FocusState state) => false;
    public Task Launch() => Task.CompletedTask;

    public Task<string> InvokeScriptAsync(string script)
    {
        InvokeScriptCalls.Add(script);
        return Task.FromResult("null");
    }

    public Task<string> InvokeMethodAsync(string method, string[] serializedArgs)
    {
        InvokeMethodCalls.Add((method, serializedArgs));
        return Task.FromResult("null");
    }

    public Task<string> InvokeScriptWithElementAsync(string script)
    {
        InvokeScriptWithElementCalls.Add(script);
        return Task.FromResult("null");
    }

    public Task PostWebMessageAsync(string json)
    {
        PostedMessages.Add(json);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Simulates receiving a message from the web view (for testing inbound channel).
    /// </summary>
    public void SimulateMessageReceived(string json)
    {
        MessageReceived?.Invoke(this, new WebViewMessageEventArgs { MessageJson = json });
    }

    // Suppress unused event warnings -- events are required by interface.
    internal void SuppressWarnings()
    {
        NewWindowRequested?.Invoke(null, null);
        NavigationStarting?.Invoke(null, null);
        NavigationCompleted?.Invoke(null, null);
        Loaded?.Invoke(null, new RoutedEventArgs());
    }
}
