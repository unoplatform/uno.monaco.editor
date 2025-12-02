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
using Windows.UI.Core;

namespace Monaco
{
    public interface ICodeEditorPresenter
	{
		// <summary>Occurs when a user performs an action in a WebView that causes content to be opened in a new window.</summary>
		event TypedEventHandler<ICodeEditorPresenter?, WebViewNewWindowRequestedEventArgs?>? NewWindowRequested;

		/// <summary>Occurs before the WebView navigates to new content.</summary>
		event TypedEventHandler<ICodeEditorPresenter?, WebViewNavigationStartingEventArgs?>? NavigationStarting;

		/// <summary>Occurs when the WebView has finished loading the current content or if navigation has failed.</summary>
		event TypedEventHandler<ICodeEditorPresenter?, WebViewNavigationCompletedEventArgs?>? NavigationCompleted;

        public CodeEditor? ParentCodeEditor { get; set; }

		public bool TriggerKeyDown(WebKeyEventArgs args);

        /// <summary>Gets or sets the Uniform Resource Identifier (URI) source of the HTML content to display in the WebView control.</summary>
        /// <returns>The Uniform Resource Identifier (URI) source of the HTML content to display in the WebView control.</returns>
        global::System.Uri Source { get; set; }

		CoreDispatcher Dispatcher { get; }

		string ElementId { get; }

		bool IsSettingValue { get; set; }

		bool IsLoaded { get; }

        event RoutedEventHandler Loaded;

		bool Focus(FocusState state);

		Task Launch();
	}
}