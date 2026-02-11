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
        public event TypedEventHandler<CodeEditor, OpenLinkRequestedEventArgs>? OpenLinkRequested;

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
                    // Emit lifecycle update via JSON-RPC for desktop testability (Task 8).
                    if (_view is DesktopCodeEditorPresenter loadingPresenter)
                    {
                        loadingPresenter.NotifyLifecycleUpdate(isLoading: true, isLoaded: false);
                    }
                    return true;

                case EditorLifecycleState.Loaded when _lifecycleState == EditorLifecycleState.Loading:
                    _lifecycleState = EditorLifecycleState.Loaded;
                    IsEditorLoaded = true;
                    EditorLoaded?.Invoke(this, new RoutedEventArgs());
                    // Emit lifecycle update via JSON-RPC for desktop testability (Task 8).
                    if (_view is DesktopCodeEditorPresenter loadedPresenter)
                    {
                        loadedPresenter.NotifyLifecycleUpdate(isLoading: false, isLoaded: true);
                    }
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

            // Guard against late callbacks after unload. Presenter handlers stay
            // attached for the presenter's lifetime, so this can fire after unload.
            if (!IsLoaded)
            {
                Debug.WriteLine("WebView_DOMContentLoaded: control not loaded, ignoring.");
                return;
            }

            if (!InitialiseWebObjects())
            {
                // Helper initialization failed -- abort. Error already surfaced via InternalException.
                return;
            }

            try
            {
                await ((ICodeEditorPresenter)sender).Launch();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"WebView_DOMContentLoaded: Launch failed: {e}");
                TeardownWebObjects();
                InternalException?.Invoke(this, e);
                return;
            }

            Options.Language = CodeLanguage;
            Options.ReadOnly = ReadOnly;
        }

        private async void WebView_NavigationCompleted(ICodeEditorPresenter? sender, PresenterNavigationCompletedEventArgs? args)
        {
#if DEBUG
            Debug.WriteLine($"Navigation completed - {args?.IsSuccess}");
#endif

            // Guard against late callbacks after unload.
            if (!IsLoaded)
            {
                Debug.WriteLine("WebView_NavigationCompleted: control not loaded, ignoring.");
                return;
            }

            // Gate on navigation success. A failed or blocked navigation should not
            // advance the lifecycle to Loaded.
            if (args is { IsSuccess: false })
            {
                Debug.WriteLine("Navigation failed — not advancing lifecycle.");
                return;
            }

            // Note: On desktop, navigation completion means the host page loaded but
            // Monaco may not be fully ready. Task 5 will add a JSON-RPC "editor/ready"
            // signal for a more precise Loaded transition. Until then, both WASM and
            // desktop use this handler as the lifecycle trigger.

            // Enable script execution before init-time calls. SendScriptAsync and
            // InvokeScriptAsync are gated by _initialized, so we must set it before
            // applying initial properties. If any script fails, InternalException
            // is surfaced by the existing try/catch in those helpers.
            _initialized = true;

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
            TransitionLifecycle(EditorLifecycleState.Loaded);
        }

        internal IParentAccessor? _parentAccessor;
        private IKeyboardListener? _keyboardListener;
        private IDebugLogger? _debugLogger;
        private long _themeToken;
        private bool _hasThemeToken;

        private void WebView_NavigationStarting(ICodeEditorPresenter? sender, PresenterNavigationStartingEventArgs? args)
        {
#if DEBUG
            Debug.WriteLine($"Navigation Starting {args?.Uri?.ToString()}");
#endif

            // Guard against late callbacks after unload.
            if (!IsLoaded)
            {
                Debug.WriteLine("WebView_NavigationStarting: control not loaded, ignoring.");
                return;
            }

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
                if (_themeListener is IDisposable disposableTheme)
                {
                    disposableTheme.Dispose();
                }
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

            // Remove static ConditionalWeakTable entries to prevent duplicate-key
            // issues on re-initialization with the same presenter instance.
            var presenterToClean = _initializedPresenter ?? _view;
            if (presenterToClean != null)
            {
                ParentAccessor.RemoveInstance(presenterToClean);
                ThemeListener.RemoveInstance(presenterToClean);
                DebugLogger.RemoveInstance(presenterToClean);
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

        /// <summary>
        /// Initializes web object wiring (bridge helpers, theme listener, etc.).
        /// Returns true on success or if already initialized for the current presenter.
        /// Returns false on failure -- caller should abort initialization.
        /// Failures are surfaced via <see cref="InternalException"/>.
        /// </summary>
        private bool InitialiseWebObjects()
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
                    return true;
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
                else if (_view is DesktopCodeEditorPresenter desktopPresenter)
                {
                    // Desktop bridge helpers created and registered as JSON-RPC targets.
                    var (parentAccessor, themeListener, keyboardListener, debugLogger) =
                        desktopPresenter.CreateBridgeTargets(_queue);
                    _parentAccessor = parentAccessor;
                    _themeListener = themeListener;
                    _keyboardListener = keyboardListener;
                    _debugLogger = debugLogger;
                }
                else
                {
                    throw new PlatformNotSupportedException(
                        $"Unsupported presenter type: {_view.GetType().Name}");
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
                return true;
            }
            catch (Exception ex)
            {
                // Roll back partial setup to prevent leaked registrations
                TeardownWebObjects();
                Debug.WriteLine($"InitialiseWebObjects Error {ex.Message} {ex.StackTrace}");
                InternalException?.Invoke(this, ex);
                return false;
            }
        }

        private async void CodeEditorLoaded()
        {
            // Guard against late callback after unload. This is invoked via
            // ParentAccessor.CallAction("Loaded") which can be queued/delayed.
            if (!IsLoaded || _lifecycleState != EditorLifecycleState.Loading)
            {
                Debug.WriteLine($"CodeEditorLoaded: ignoring (IsLoaded={IsLoaded}, state={_lifecycleState})");
                return;
            }

            _view = _view ?? throw new InvalidOperationException("The view not set");

            // Enable script execution before init-time calls. SendScriptAsync and
            // InvokeScriptAsync are gated by _initialized, so we must set it before
            // applying initial properties.
            _initialized = true;

            // Make sure inner editor is focused
            await SendScriptAsync("EditorContext.getEditorForElement(element).editor.focus();");

            await SendScriptAsync("EditorContext.getEditorForElement(element).editor.layout();");

            // Apply all current property values in the correct order
            // This ensures properties set before IsEditorLoaded=true take effect
            await ApplyInitialPropertyValues();

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

            // 2. Apply content after language is configured.
            // Always send values (including empty string) so Monaco state is
            // synchronized on reload -- skipping empty would leave stale content.
            await InvokeScriptAsync("updateContent", Text ?? string.Empty);
            await InvokeScriptAsync("updateSelectedContent", SelectedText ?? string.Empty);

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

        private void WebView_NewWindowRequested(ICodeEditorPresenter? sender, PresenterNewWindowRequestedEventArgs? args)
        {
            if (sender is not null)
            {
                var linkArgs = new OpenLinkRequestedEventArgs
                {
                    Uri = args?.Uri
                };
                OpenLinkRequested?.Invoke(this, linkArgs);
                // Propagate handled state back to presenter args
                if (args is not null)
                {
                    args.Handled = linkArgs.Handled;
                }
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
