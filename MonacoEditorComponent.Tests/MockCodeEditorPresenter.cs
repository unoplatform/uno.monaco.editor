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

    public event TypedEventHandler<ICodeEditorPresenter?, PresenterNewWindowRequestedEventArgs?>? NewWindowRequested;
    public event TypedEventHandler<ICodeEditorPresenter?, PresenterNavigationStartingEventArgs?>? NavigationStarting;
    public event TypedEventHandler<ICodeEditorPresenter?, PresenterNavigationCompletedEventArgs?>? NavigationCompleted;
    public event EventHandler<WebViewMessageEventArgs>? MessageReceived;
    public event RoutedEventHandler? Loaded;

    public CodeEditor? ParentCodeEditor { get; set; }
    public global::System.Uri Source { get; set; } = new global::System.Uri("about:blank");
    public DispatcherQueue DispatcherQueue => DispatcherQueue.GetForCurrentThread()!;
    public string ElementId => "mock-presenter";
    public bool IsSettingValue { get; set; }
    public bool IsLoaded => true;

    public bool TriggerKeyDown(WebKeyEventArgs args) => false;
    public bool Focus(FocusState state) => false;
    public Task Launch() => Task.CompletedTask;
    public Task<string> InvokeScriptAsync(string script) => Task.FromResult("null");
    public Task<string> InvokeMethodAsync(string method, string[] serializedArgs) => Task.FromResult("null");
    public Task<string> InvokeScriptWithElementAsync(string script) => Task.FromResult("null");

    public void PostWebMessage(string json)
    {
        PostedMessages.Add(json);
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
