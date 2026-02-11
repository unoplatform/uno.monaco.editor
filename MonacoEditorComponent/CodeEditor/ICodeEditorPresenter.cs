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

    public interface ICodeEditorPresenter
	{
		// <summary>Occurs when a user performs an action in a WebView that causes content to be opened in a new window.</summary>
		event TypedEventHandler<ICodeEditorPresenter?, WebViewNewWindowRequestedEventArgs?>? NewWindowRequested;

		/// <summary>Occurs before the WebView navigates to new content.</summary>
		event TypedEventHandler<ICodeEditorPresenter?, WebViewNavigationStartingEventArgs?>? NavigationStarting;

		/// <summary>Occurs when the WebView has finished loading the current content or if navigation has failed.</summary>
		event TypedEventHandler<ICodeEditorPresenter?, WebViewNavigationCompletedEventArgs?>? NavigationCompleted;

        /// <summary>
        /// Inbound message event from the web view layer.
        /// Desktop presenter fires this from WebMessageReceived.
        /// WASM presenter never fires it (uses JSExport direct calls).
        /// Task 5 consumes this for all desktop bridge routing.
        /// </summary>
        event EventHandler<WebViewMessageEventArgs>? MessageReceived;

        public CodeEditor? ParentCodeEditor { get; set; }

		public bool TriggerKeyDown(WebKeyEventArgs args);

        /// <summary>Gets or sets the Uniform Resource Identifier (URI) source of the HTML content to display in the WebView control.</summary>
        /// <returns>The Uniform Resource Identifier (URI) source of the HTML content to display in the WebView control.</returns>
        global::System.Uri Source { get; set; }

		DispatcherQueue DispatcherQueue { get; }

		string ElementId { get; }

		bool IsSettingValue { get; set; }

		bool IsLoaded { get; }

        event RoutedEventHandler Loaded;

		bool Focus(FocusState state);

		Task Launch();

        /// <summary>
        /// Executes a script in the web view and returns the result as a raw JSON token string.
        /// WASM wraps NativeMethods.InvokeJS(). Desktop wraps CoreWebView2.ExecuteScriptAsync().
        /// Return contract: always returns raw JSON token (string, number, object, null).
        /// </summary>
        Task<string> InvokeScriptAsync(string script);

        /// <summary>
        /// Posts a JSON message to the web view.
        /// Desktop wraps CoreWebView2.PostWebMessageAsJson().
        /// WASM throws PlatformNotSupportedException (not used on WASM).
        /// </summary>
        void PostWebMessage(string json);
	}
}
