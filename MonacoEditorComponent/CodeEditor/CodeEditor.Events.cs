using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Monaco.Helpers;

using System.Diagnostics;
using System.Reflection;

using Windows.Foundation;
using Windows.UI.Core;

namespace Monaco
{
    public partial class CodeEditor
    {
        // Override default Loaded/Loading event so we can make sure we've initialized our WebView contents with the CodeEditor.

        /// <summary>
        /// When Editor is Loading, it is ready to receive commands to the Monaco Engine.
        /// </summary>
        public event RoutedEventHandler? EditorLoading;

        /// <summary>
        /// When Editor is Loaded, it has been rendered and is ready to be displayed.
        /// </summary>
        public event RoutedEventHandler? EditorLoaded;

        /// <summary>
        /// Called when a link is Ctrl+Clicked on in the editor, set Handled to true to prevent opening.
        /// </summary>
        public event TypedEventHandler<ICodeEditorPresenter, WebViewNewWindowRequestedEventArgs>? OpenLinkRequested;

        /// <summary>
        /// Called when an internal exception is encountered while executing a command. (for testing/reporting issues)
        /// </summary>
        public event TypedEventHandler<CodeEditor, Exception>? InternalException;

        /// <summary>
        /// Custom Keyboard Handler.
        /// </summary>
        public new event WebKeyEventHandler? KeyDown;

        private ThemeListener? _themeListener;

        private void WebView_DOMContentLoaded(object sender, RoutedEventArgs args)
            => WebView_DOMContentLoaded();

        private void WebView_DOMContentLoaded()
        {
#if DEBUG
            Console.WriteLine("WebView_DOMContentLoaded()");
#endif
            // Don't set _initialized here - let ReplayQueuedPropertyChanges do it atomically
            // This prevents properties from executing immediately before the queue is replayed

#if __WASM__
            InitialiseWebObjects();

            _ = _view?.Launch();

            // Don't update Options here - the queued property changes will handle it
            // Options.Language = CodeLanguage;
            // Options.ReadOnly = ReadOnly;
#endif
        }

        private async void WebView_NavigationCompleted(ICodeEditorPresenter? sender, WebViewNavigationCompletedEventArgs? args)
        {
#if DEBUG && !HAS_UNO_WASM
            Debug.WriteLine($"Navigation completed - {args?.IsSuccess}");
#endif
            IsEditorLoaded = true;

            // Make sure inner editor is focused
            await SendScriptAsync("EditorContext.getEditorForElement(element).editor.focus();");

            // If we're supposed to have focus, make sure we try and refocus on our now loaded webview.
#pragma warning disable CS0618 // Type or member is obsolete
            if (FocusManager.GetFocusedElement() == this)
            {
                _view?.Focus(FocusState.Programmatic);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            EditorLoaded?.Invoke(this, new RoutedEventArgs());
        }

        internal ParentAccessor? _parentAccessor;
        private KeyboardListener? _keyboardListener;
        private DebugLogger? _debugLogger;
        private long _themeToken;

        private void WebView_NavigationStarting(ICodeEditorPresenter? sender, WebViewNavigationStartingEventArgs? args)
        {
#if DEBUG
            Debug.WriteLine($"Navigation Starting {args?.Uri?.ToString()}");
#endif
            InitialiseWebObjects();
        }

        private void InitialiseWebObjects()
        {
            try
            {
                _queue = _queue ?? throw new InvalidOperationException("DispatcherQueue not set");

                if (_view == null)
                {
                    throw new InvalidOperationException("Unable to find CodeEditorPresenter");
                }

                _parentAccessor = new ParentAccessor(_view, _queue);
                _parentAccessor.AddAssemblyForTypeLookup(typeof(Range).GetTypeInfo().Assembly);
                _parentAccessor.RegisterAction("Loaded", CodeEditorLoaded);

                _themeListener = new ThemeListener(_view);
                _themeListener.ThemeChanged += ThemeListener_ThemeChanged;
                _themeToken = RegisterPropertyChangedCallback(RequestedThemeProperty, RequestedTheme_PropertyChanged);

                _keyboardListener = new KeyboardListener(_view, _queue);
                _debugLogger = new DebugLogger(_view);

                Debug.WriteLine($"InitialiseWebObjects - Completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialiseWebObjects Error {ex.Message} {ex.StackTrace}");
            }
        }

        private async void CodeEditorLoaded()
        {
            _view = _view ?? throw new InvalidOperationException("The view not set");

            // Now we're done loading
            EditorLoading?.Invoke(this, new RoutedEventArgs());

            // Make sure inner editor is focused
            await SendScriptAsync("EditorContext.getEditorForElement(element).editor.focus();");

            await SendScriptAsync("EditorContext.getEditorForElement(element).editor.layout();");

            // Replay any property changes that occurred before initialization
            // This will handle Text, SelectedText, CodeLanguage, ReadOnly, HasGlyphMargin, Decorations, Markers, and any other queued changes
            // ReplayQueuedPropertyChanges() will set _initialized = true atomically with copying the queue
            await ReplayQueuedPropertyChanges();

            // If we're supposed to have focus, make sure we try and refocus on our now loaded webview.
#pragma warning disable CS0618 // Type or member is obsolete
            if (FocusManager.GetFocusedElement() == this)
            {
                _view.Focus(FocusState.Programmatic);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            IsEditorLoaded = true;

            EditorLoaded?.Invoke(this, new RoutedEventArgs());

#if __WASM__
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => WebView_NavigationCompleted(_view, null));
#endif
        }

        private void WebView_NewWindowRequested(ICodeEditorPresenter? sender, WebViewNewWindowRequestedEventArgs? args)
        {
            if (sender is not null && args is not null)
            {
                // TODO: Should probably create own event args here as we don't want to expose the referrer to our internal page?
                OpenLinkRequested?.Invoke(sender, args);
            }
        }

        private async void RequestedTheme_PropertyChanged(DependencyObject? obj, DependencyProperty property)
        {
            if (obj is CodeEditor editor
                && _themeListener is { } listener)
            {
                var theme = editor.RequestedTheme;
                var tstr = string.Empty;

                if (theme == ElementTheme.Default)
                {
                    tstr = _themeListener?.CurrentThemeName;
                }
                else
                {
                    tstr = theme.ToString();
                }

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    await InvokeScriptAsync("changeTheme", [tstr ?? "", listener.IsHighContrast.ToString()]);
                });
            }
        }

        private async void ThemeListener_ThemeChanged(ThemeListener sender)
        {
            if (RequestedTheme == ElementTheme.Default)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    await InvokeScriptAsync("changeTheme", args: [sender.CurrentTheme.ToString(), sender.IsHighContrast.ToString()]);
                });
            }
        }

        internal bool TriggerKeyDown(WebKeyEventArgs args)
        {
            KeyDown?.Invoke(this, args);

            return args.Handled;
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);

#pragma warning disable CS0618 // Type or member is obsolete
            if (_view != null && FocusManager.GetFocusedElement() == this)
            {
                // Forward Focus onto our inner WebView
                _view.Focus(FocusState.Programmatic);
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
