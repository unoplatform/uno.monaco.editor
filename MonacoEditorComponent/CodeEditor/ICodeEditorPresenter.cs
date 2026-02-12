using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Monaco.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;

namespace Monaco
{
    /// <summary>
    /// Event args for inbound messages from the web view layer.
    /// Desktop presenter fires this from WebMessageReceived.
    /// WASM presenter never fires it (uses JSExport direct calls).
    /// </summary>
    public sealed class WebViewMessageEventArgs : EventArgs
    {
        /// <summary>
        /// The raw JSON string of the inbound message.
        /// </summary>
        public required string MessageJson { get; init; }
    }

    /// <summary>
    /// Cross-platform event args for link open requests.
    /// Wraps platform-specific WebView args into a portable type.
    /// </summary>
    public sealed class OpenLinkRequestedEventArgs : EventArgs
    {
        /// <summary>
        /// The URI of the link that was requested to be opened.
        /// May be null if the platform does not provide the URI.
        /// </summary>
        public global::System.Uri? Uri { get; init; }

        /// <summary>
        /// Set to true to prevent the default navigation behavior.
        /// </summary>
        public bool Handled { get; set; }
    }

    /// <summary>
    /// Portable event args for navigation completion.
    /// Replaces WinRT WebViewNavigationCompletedEventArgs which cannot be constructed.
    /// </summary>
    public sealed class PresenterNavigationCompletedEventArgs : EventArgs
    {
        /// <summary>Whether the navigation completed successfully.</summary>
        public bool IsSuccess { get; init; }
    }

    /// <summary>
    /// Portable event args for new-window (link open) requests from the presenter layer.
    /// Replaces WinRT WebViewNewWindowRequestedEventArgs which cannot be constructed.
    /// </summary>
    public sealed class PresenterNewWindowRequestedEventArgs : EventArgs
    {
        /// <summary>The URI of the requested navigation.</summary>
        public global::System.Uri? Uri { get; init; }

        /// <summary>Set to true to mark the request as handled.</summary>
        public bool Handled { get; set; }
    }

    /// <summary>
    /// Portable event args for navigation starting from the presenter layer.
    /// Replaces WinRT WebViewNavigationStartingEventArgs which cannot be constructed
    /// in cross-platform code. Provides the navigation URI and cancel capability.
    /// </summary>
    public sealed class PresenterNavigationStartingEventArgs : EventArgs
    {
        /// <summary>The URI of the navigation being started.</summary>
        public global::System.Uri? Uri { get; init; }

        /// <summary>Set to true to cancel the navigation.</summary>
        public bool Cancel { get; set; }
    }

    /// <summary>
    /// Defines the cross-platform contract for the editor presenter that hosts the Monaco
    /// web content. WASM uses <see cref="WasmCodeEditorPresenter"/> (native browser element);
    /// desktop uses <see cref="DesktopCodeEditorPresenter"/> (WebView2 with JSON-RPC bridge).
    /// </summary>
    public interface ICodeEditorPresenter
	{
		/// <summary>Occurs when a user performs an action in a WebView that causes content to be opened in a new window.</summary>
		event TypedEventHandler<ICodeEditorPresenter?, PresenterNewWindowRequestedEventArgs?>? NewWindowRequested;

		/// <summary>Occurs before the WebView navigates to new content.</summary>
		event TypedEventHandler<ICodeEditorPresenter?, PresenterNavigationStartingEventArgs?>? NavigationStarting;

		/// <summary>Occurs when the WebView has finished loading the current content or if navigation has failed.</summary>
		event TypedEventHandler<ICodeEditorPresenter?, PresenterNavigationCompletedEventArgs?>? NavigationCompleted;

        /// <summary>
        /// Inbound message event from the web view layer.
        /// Desktop presenter fires this from WebMessageReceived.
        /// WASM presenter never fires it (uses JSExport direct calls).
        /// Task 5 consumes this for all desktop bridge routing.
        /// </summary>
        event EventHandler<WebViewMessageEventArgs>? MessageReceived;

        /// <summary>Gets or sets the parent <see cref="CodeEditor"/> that owns this presenter.</summary>
        public CodeEditor? ParentCodeEditor { get; set; }

		/// <summary>Routes a key-down event from JavaScript to the parent editor's <see cref="CodeEditor.KeyDown"/> handler.</summary>
		/// <param name="args">The key event arguments.</param>
		/// <returns><see langword="true"/> if the event was handled; otherwise, <see langword="false"/>.</returns>
		public bool TriggerKeyDown(WebKeyEventArgs args);

        /// <summary>Gets or sets the Uniform Resource Identifier (URI) source of the HTML content to display in the WebView control.</summary>
        /// <returns>The Uniform Resource Identifier (URI) source of the HTML content to display in the WebView control.</returns>
        global::System.Uri Source { get; set; }

		/// <summary>Gets the <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/> for the UI thread.</summary>
		DispatcherQueue DispatcherQueue { get; }

		/// <summary>Gets the unique HTML element identifier for this presenter instance.</summary>
		string ElementId { get; }

		/// <summary>Gets or sets a value indicating whether the bridge is currently pushing a value, suppressing re-entrant change notifications.</summary>
		bool IsSettingValue { get; set; }

		/// <summary>Gets a value indicating whether this presenter element is loaded in the visual tree.</summary>
		bool IsLoaded { get; }

		/// <summary>Occurs when the presenter element is loaded in the visual tree.</summary>
        event RoutedEventHandler Loaded;

		/// <summary>Attempts to set focus on the presenter.</summary>
		/// <param name="state">The focus state to apply.</param>
		/// <returns><see langword="true"/> if focus was successfully set.</returns>
		bool Focus(FocusState state);

		/// <summary>
		/// Initializes the underlying web content host (WebView2 on desktop, BrowserHtmlElement
		/// on WASM) and starts the Monaco editor bootstrap sequence.
		/// </summary>
		Task Launch();

        /// <summary>
        /// Executes a script in the web view and returns the result as a raw JSON token string.
        /// WASM wraps NativeMethods.InvokeJS(). Desktop wraps CoreWebView2.ExecuteScriptAsync().
        /// Return contract: always returns raw JSON token (string, number, object, null).
        /// </summary>
        Task<string> InvokeScriptAsync(string script);

        /// <summary>
        /// Invokes a named JavaScript function with pre-serialized arguments, automatically
        /// resolving the editor element reference per-platform. Callers never reference
        /// <c>element</c> directly -- the presenter injects it.
        /// </summary>
        /// <param name="method">The global function name (e.g., <c>"updateContent"</c>).</param>
        /// <param name="serializedArgs">Pre-serialized argument strings (JSON literals or raw values).</param>
        /// <returns>The raw JSON result string from the function call.</returns>
        Task<string> InvokeMethodAsync(string method, string[] serializedArgs);

        /// <summary>
        /// Executes a raw script that references <c>element</c>, with the presenter
        /// automatically defining the <c>element</c> variable per-platform before evaluation.
        /// Use this for ad-hoc scripts that reference <c>EditorContext.getEditorForElement(element)</c>.
        /// Prefer <see cref="InvokeMethodAsync"/> for named function calls.
        /// </summary>
        /// <param name="script">The raw JavaScript to execute. May reference <c>element</c>.</param>
        /// <returns>The raw JSON result string.</returns>
        Task<string> InvokeScriptWithElementAsync(string script);

        /// <summary>
        /// Posts a JSON message to the web view.
        /// Desktop wraps CoreWebView2.PostWebMessageAsJson().
        /// WASM throws PlatformNotSupportedException (not used on WASM).
        /// </summary>
        void PostWebMessage(string json);
	}
}
