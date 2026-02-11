using System.Diagnostics;
using System.Reflection;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Monaco.Helpers;

using Windows.Foundation;

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

        private IThemeListener? _themeListener;
        private EditorLifecycleState _lifecycleState = EditorLifecycleState.Unloaded;

        /// <summary>
        /// Transitions the editor lifecycle to the specified state,
        /// firing EditorLoading/EditorLoaded exactly once per transition.
        /// Returns true if the transition was valid and executed.
        /// </summary>
        private bool TransitionLifecycle(EditorLifecycleState targetState)
        {
            // Only allow valid forward transitions
            switch (targetState)
            {
                case EditorLifecycleState.Loading when _lifecycleState == EditorLifecycleState.Unloaded:
                    _lifecycleState = EditorLifecycleState.Loading;
                    EditorLoading?.Invoke(this, new RoutedEventArgs());
                    return true;

                case EditorLifecycleState.Loaded when _lifecycleState == EditorLifecycleState.Loading:
                    _lifecycleState = EditorLifecycleState.Loaded;
                    IsEditorLoaded = true;
                    EditorLoaded?.Invoke(this, new RoutedEventArgs());
                    return true;

                case EditorLifecycleState.Unloaded:
                    _lifecycleState = EditorLifecycleState.Unloaded;
                    IsEditorLoaded = false;
                    return true;

                default:
                    Debug.WriteLine($"Invalid lifecycle transition: {_lifecycleState} -> {targetState}");
                    return false;
            }
        }

        private async void WebView_DOMContentLoaded(object sender, RoutedEventArgs args)
        {
#if DEBUG
            Console.WriteLine("WebView_DOMContentLoaded()");
#endif

            InitialiseWebObjects();

            await ((ICodeEditorPresenter)sender).Launch();

            Options.Language = CodeLanguage;
            Options.ReadOnly = ReadOnly;
        }

        private async void WebView_NavigationCompleted(ICodeEditorPresenter? sender, WebViewNavigationCompletedEventArgs? args)
        {
#if DEBUG
            Debug.WriteLine($"Navigation completed - {args?.IsSuccess}");
#endif

            // On desktop, navigation completion means the host page has loaded but
            // Monaco may not be ready yet. Task 5 adds a JSON-RPC "editor/ready"
            // signal that gates the Loaded transition on actual Monaco readiness.
            // Until then, desktop relies on the WASM path through CodeEditorLoaded()
            // (called by ParentAccessor) which only fires after Monaco is initialized.
            // This NavigationCompleted handler is the WASM fallback path.
            if (!OperatingSystem.IsBrowser())
            {
                // Desktop: skip this handler — lifecycle is driven by CodeEditorLoaded()
                // which fires when Monaco calls the "Loaded" parent accessor action.
                return;
            }

            // Make sure inner editor is focused
            await SendScriptAsync("EditorContext.getEditorForElement(element).editor.focus();");

            // If we're supposed to have focus, make sure we try and refocus on our now loaded webview.
#pragma warning disable CS0618 // Type or member is obsolete
            if (FocusManager.GetFocusedElement() == this)
            {
                _view?.Focus(FocusState.Programmatic);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            await ApplyInitialPropertyValues();

            // Use lifecycle state machine for exactly-once semantics
            _initialized = true;
            TransitionLifecycle(EditorLifecycleState.Loaded);
        }

        internal IParentAccessor? _parentAccessor;
        private IKeyboardListener? _keyboardListener;
        private IDebugLogger? _debugLogger;
        private long _themeToken;
        private bool _hasThemeToken;

        private void WebView_NavigationStarting(ICodeEditorPresenter? sender, WebViewNavigationStartingEventArgs? args)
        {
#if DEBUG
            Debug.WriteLine($"Navigation Starting {args?.Uri?.ToString()}");
#endif
            InitialiseWebObjects();
        }

        private ICodeEditorPresenter? _initializedPresenter;

        /// <summary>
        /// Tears down web object wiring based on actual field state.
        /// Every cleanup action is guarded by its own field, so partial
        /// initialization is always fully rolled back.
        /// </summary>
        private void TeardownWebObjects()
        {
            if (_themeListener != null)
            {
                _themeListener.ThemeChanged -= ThemeListener_ThemeChanged;
                _themeListener = null;
            }

            if (_hasThemeToken)
            {
                UnregisterPropertyChangedCallback(RequestedThemeProperty, _themeToken);
                _hasThemeToken = false;
            }

            if (_initializedPresenter != null)
            {
                KeyboardListener.RemoveInstance(_initializedPresenter);
            }
            else if (_view != null && _keyboardListener != null)
            {
                // Partial init: keyboard was registered against _view but
                // _initializedPresenter was never assigned
                KeyboardListener.RemoveInstance(_view);
            }

            if (_parentAccessor is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _parentAccessor = null;
            _keyboardListener = null;
            _debugLogger = null;
            _initializedPresenter = null;

            // Reset lifecycle state on teardown via transition method
            TransitionLifecycle(EditorLifecycleState.Unloaded);
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

                // Skip if already initialized for this presenter instance.
                if (ReferenceEquals(_initializedPresenter, _view))
                {
                    return;
                }

                // Teardown old or partial objects before reinitializing
                TeardownWebObjects();

                if (OperatingSystem.IsBrowser())
                {
                    var (parentAccessor, themeListener, keyboardListener, debugLogger) =
                        BridgeFactory.Create(_view, _queue);
                    _parentAccessor = parentAccessor;
                    _themeListener = themeListener;
                    _keyboardListener = keyboardListener;
                    _debugLogger = debugLogger;
                }
                else
                {
                    // Desktop bridge helpers are created by Task 5.
                    // For now, create minimal wiring without bridge factory.
                    _themeListener = new ThemeListener(_view);
                    // _parentAccessor, _keyboardListener, _debugLogger remain null
                    // until Task 5 provides desktop implementations.
                }

                _parentAccessor?.AddAssemblyForTypeLookup(typeof(Range).GetTypeInfo().Assembly);
                _parentAccessor?.RegisterAction("Loaded", CodeEditorLoaded);

                _themeListener.ThemeChanged += ThemeListener_ThemeChanged;
                _themeToken = RegisterPropertyChangedCallback(RequestedThemeProperty, RequestedTheme_PropertyChanged);
                _hasThemeToken = true;

                _initializedPresenter = _view;

                // Transition to Loading state
                TransitionLifecycle(EditorLifecycleState.Loading);

                Debug.WriteLine($"InitialiseWebObjects - Completed");
            }
            catch (Exception ex)
            {
                // Roll back partial setup to prevent leaked registrations
                TeardownWebObjects();
                Debug.WriteLine($"InitialiseWebObjects Error {ex.Message} {ex.StackTrace}");
            }
        }

        private async void CodeEditorLoaded()
        {
            _view = _view ?? throw new InvalidOperationException("The view not set");

            // Make sure inner editor is focused
            await SendScriptAsync("EditorContext.getEditorForElement(element).editor.focus();");

            await SendScriptAsync("EditorContext.getEditorForElement(element).editor.layout();");

            // Apply all current property values in the correct order
            // This ensures properties set before IsEditorLoaded=true take effect
            await ApplyInitialPropertyValues();

            // Now mark as initialized
            _initialized = true;

            // If we're supposed to have focus, make sure we try and refocus on our now loaded webview.
#pragma warning disable CS0618 // Type or member is obsolete
            if (FocusManager.GetFocusedElement() == this)
            {
                _view.Focus(FocusState.Programmatic);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            // Use lifecycle state machine for exactly-once semantics
            TransitionLifecycle(EditorLifecycleState.Loaded);
        }

        /// <summary>
        /// Applies all current property values to Monaco in the correct order.
        /// Called during initialization to ensure properties set before IsEditorLoaded=true take effect.
        /// Order matters: language/options must be set before content for proper syntax highlighting.
        /// </summary>
        private async Task ApplyInitialPropertyValues()
        {
            // 1. Apply language and options first
            if (!string.IsNullOrEmpty(CodeLanguage))
            {
                await InvokeScriptAsync("updateLanguage", CodeLanguage);
            }

            await InvokeScriptAsync("updateOptions", Options);

            // 2. Apply content after language is configured
            if (!string.IsNullOrEmpty(Text))
            {
                await InvokeScriptAsync("updateContent", Text);
            }

            if (!string.IsNullOrEmpty(SelectedText))
            {
                await InvokeScriptAsync("updateSelectedContent", SelectedText);
            }

            // 3. Apply decorations and markers last
            if (Decorations != null && Decorations.Count > 0)
            {
                await DeltaDecorationsHelperAsync([.. Decorations]);
            }

            if (Markers != null && Markers.Count > 0)
            {
                await SetModelMarkersAsync("CodeEditor", [.. Markers]);
            }
        }

        private void WebView_NewWindowRequested(ICodeEditorPresenter? sender, WebViewNewWindowRequestedEventArgs? args)
        {
            if (sender is not null)
            {
                // On desktop, args is null because WebViewNewWindowRequestedEventArgs is a WinRT
                // type that cannot be constructed. The desktop presenter already blocks the navigation
                // (args.Handled = true). On WASM, args comes from the framework with Uri/Referrer.
                OpenLinkRequested?.Invoke(sender, args!);
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

                if (!_queue!.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                {
                    await InvokeScriptAsync("changeTheme", [tstr ?? "", listener.IsHighContrast.ToString()]);
                }))
                {
                    Debug.WriteLine("Failed to enqueue theme change -- dispatcher queue unavailable");
                }
            }
        }

        private void ThemeListener_ThemeChanged(IThemeListener sender)
        {
            if (RequestedTheme == ElementTheme.Default)
            {
                if (!_queue!.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                {
                    await InvokeScriptAsync("changeTheme", args: [sender.CurrentTheme.ToString(), sender.IsHighContrast.ToString()]);
                }))
                {
                    Debug.WriteLine("Failed to enqueue theme change -- dispatcher queue unavailable");
                }
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
