using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Monaco.Helpers;
using Monaco.Serialization;

using Windows.Foundation;

namespace Monaco
{
    public partial class CodeEditor
    {
        // Override default Loaded/Loading event so we can make sure we've initialized our WebView contents with the CodeEditor.

        /// <summary>
        /// Occurs when the editor begins initialization. The Monaco instance is not yet
        /// available and script calls are still gated. Fires exactly once per initialization cycle.
        /// </summary>
        public event RoutedEventHandler? EditorLoading;

        /// <summary>
        /// Occurs when the editor has fully loaded and rendered the Monaco instance.
        /// Fires exactly once per initialization cycle, after <see cref="EditorLoading"/>.
        /// </summary>
        public event RoutedEventHandler? EditorLoaded;

        /// <summary>
        /// Occurs when a link is Ctrl+Clicked in the editor. Set
        /// <see cref="OpenLinkRequestedEventArgs.Handled"/> to <see langword="true"/> to
        /// prevent the default navigation behavior.
        /// </summary>
        public event TypedEventHandler<CodeEditor, OpenLinkRequestedEventArgs>? OpenLinkRequested;

        /// <summary>
        /// Occurs when an internal exception is encountered while executing a script command.
        /// Subscribe to this event for diagnostics and error reporting.
        /// </summary>
        public event TypedEventHandler<CodeEditor, Exception>? InternalException;

        /// <summary>
        /// Occurs when a key is pressed inside the Monaco editor.
        /// Shadows <see cref="Microsoft.UI.Xaml.UIElement.KeyDown"/> to provide
        /// Monaco-specific key event arguments.
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
                var presenter = (ICodeEditorPresenter)sender;
                await presenter.Launch();
                presenter.Loaded -= WebView_DOMContentLoaded;
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
            DesktopCodeEditorPresenter.DiagnosticLog(
                $"WebView_NavigationCompleted: IsSuccess={args?.IsSuccess}, IsLoaded={IsLoaded}, " +
                $"lifecycle={_lifecycleState}, initialized={_initialized}, isDesktop={_view is DesktopCodeEditorPresenter}");
#if DEBUG
            Debug.WriteLine($"Navigation completed - {args?.IsSuccess}");
#endif

            // Guard against late callbacks after unload.
            if (!IsLoaded)
            {
                DesktopCodeEditorPresenter.DiagnosticLog("WebView_NavigationCompleted: control not loaded, ignoring.");
                return;
            }

            // Gate on navigation success. A failed or blocked navigation should not
            // advance the lifecycle to Loaded.
            if (args is { IsSuccess: false })
            {
                DesktopCodeEditorPresenter.DiagnosticLog("WebView_NavigationCompleted: navigation failed, not advancing lifecycle.");
                return;
            }

            // On desktop, navigation completion means editor.html and the JS bundle have loaded.
            // Initialize the Monaco editor by calling createMonacoEditor(), which registers the
            // editor context and fires the "Loaded" callback (CodeEditorLoaded) when complete.
            // This is the desktop equivalent of WasmCodeEditorPresenter.Launch() calling
            // NativeMethods.InitializeMonaco(). The CodeEditorLoaded callback handles
            // _initialized, property application, and lifecycle transitions.
            //
            // Idempotency guard: only invoke createMonacoEditor when lifecycle is Loading
            // (set by InitialiseWebObjects) and the editor has not yet been initialized.
            // This prevents duplicate bootstrap on repeated navigations or WebView reloads.
            if (_view is DesktopCodeEditorPresenter desktopPresenter
                && ShouldInvokeDesktopBootstrap(_lifecycleState, _initialized, _desktopBootstrapInFlight))
            {
                // Build initial state to push to JS -- eliminates async RPC round-trips.
                var initialStateJson = BuildInitialStateJson();
                var escapedState = JsonSerializer.Serialize(initialStateJson);
                StartDesktopBootstrap(desktopPresenter, escapedState, "WebView_NavigationCompleted");

                return;
            }
            else
            {
                DesktopCodeEditorPresenter.DiagnosticLog(
                    $"WebView_NavigationCompleted: skipped createMonacoEditor (guard failed, bootstrapInFlight={_desktopBootstrapInFlight})");
            }

            // WASM path: NavigationCompleted does not fire on WASM (BrowserHtmlElement
            // does not emit this event). This code is a legacy fallback for any future
            // presenter that does emit NavigationCompleted.
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

        /// <summary>
        /// Builds a JSON string containing the initial editor state (theme, text,
        /// language, options) that C# pushes to <c>createMonacoEditor</c> on desktop.
        /// This eliminates async RPC round-trips during init -- JS uses the provided
        /// values directly instead of calling back to C# for each property.
        /// </summary>
        private string BuildInitialStateJson()
        {
            var themeName = RequestedTheme == ElementTheme.Default
                ? _themeListener?.CurrentThemeName ?? "Light"
                : RequestedTheme.ToString();
            var isHighContrast = _themeListener?.IsHighContrast ?? false;
            var requestedTheme = (int)RequestedTheme;

            // Build a JSON object with all the state JS needs at init time.
            // Using raw JSON construction to avoid needing another STJ context.
            var text = Text ?? string.Empty;
            var language = CodeLanguage ?? "plaintext";
            var readOnly = ReadOnly;

            // Use FallbackOptions (reflection-based) since anonymous types are not registered
            // in MonacoJsonContext. Safe on desktop (native code, not AOT-WASM).
            var json = JsonSerializer.Serialize(new
            {
                requestedTheme,
                themeName,
                isHighContrast,
                text,
                language,
                readOnly
            }, MonacoJsonContext.FallbackOptions);

            DesktopCodeEditorPresenter.DiagnosticLog($"BuildInitialStateJson: {json}");
            return json;
        }

        /// <summary>
        /// Re-bootstraps Monaco on an existing WebView2 that is already navigated to
        /// editor.html. Called when the presenter is reused after a hard teardown
        /// (bridge was torn down but WebView2 is still healthy). The bridge has already
        /// been restored via <see cref="InitialiseWebObjects"/> before this is called.
        /// </summary>
        private void RebootstrapMonacoAsync()
        {
            if (_view is not DesktopCodeEditorPresenter desktopPresenter)
            {
                return;
            }

            if (_desktopBootstrapInFlight)
            {
                DesktopCodeEditorPresenter.DiagnosticLog("RebootstrapMonacoAsync: skipped (bootstrap already in-flight)");
                return;
            }

            // Build initial state to push to JS -- eliminates async RPC round-trips.
            var initialStateJson = BuildInitialStateJson();
            var escapedState = JsonSerializer.Serialize(initialStateJson);
            StartDesktopBootstrap(desktopPresenter, escapedState, "RebootstrapMonacoAsync");
        }

        private void StartDesktopBootstrap(ICodeEditorPresenter presenter, string escapedState, string source)
        {
            _desktopBootstrapInFlight = true;
            DesktopCodeEditorPresenter.DiagnosticLog($"{source}: invoking createMonacoEditor (fire-and-forget)...");
            _ = InvokeDesktopBootstrapAsync(presenter, escapedState, source);

            // Timeout fallback: if CodeEditorLoaded never fires (script failure,
            // Monaco crash, etc.), surface a diagnostic error after 30 seconds.
            _ = MonitorInitTimeoutAsync();
        }

        private async Task InvokeDesktopBootstrapAsync(ICodeEditorPresenter presenter, string escapedState, string source)
        {
            try
            {
                await presenter.InvokeScriptAsync(BuildCreateMonacoEditorScript(escapedState));
                DesktopCodeEditorPresenter.DiagnosticLog($"{source}: createMonacoEditor invoked on desktop");
            }
            catch (Exception ex)
            {
                _desktopBootstrapInFlight = false;
                DesktopCodeEditorPresenter.DiagnosticLog($"{source}: createMonacoEditor failed: {ex}");
                InternalException?.Invoke(this, ex);
            }
        }

        internal static string BuildCreateMonacoEditorScript(string escapedState)
            => $"void createMonacoEditor(null, 'editor-container', '', {escapedState})";

        /// <summary>
        /// Timeout fallback: if CodeEditorLoaded never fires within 30 seconds
        /// after createMonacoEditor was invoked, surface a diagnostic error.
        /// This detects script failures, Monaco crashes, or bridge disconnects
        /// that prevent the "Loaded" callback from firing.
        /// </summary>
        private async Task MonitorInitTimeoutAsync()
        {
            const int timeoutMs = 30_000;
            var startState = _lifecycleState;
            await Task.Delay(timeoutMs);

            // Only report if we're still stuck in Loading (not yet Loaded or Unloaded).
            if (_lifecycleState == EditorLifecycleState.Loading && startState == EditorLifecycleState.Loading)
            {
                _desktopBootstrapInFlight = false;
                var msg = $"Monaco editor initialization timed out after {timeoutMs}ms. " +
                    "CodeEditorLoaded callback was never received. Check browser console for errors.";
                DesktopCodeEditorPresenter.DiagnosticLog($"INIT_TIMEOUT: {msg}");
                InternalException?.Invoke(this, new TimeoutException(msg));
            }
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
            _desktopBootstrapInFlight = false;

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
            Debug.WriteLine($"CodeEditorLoaded: IsLoaded={IsLoaded}, state={_lifecycleState}, HasThreadAccess={_queue?.HasThreadAccess}");

            // Guard against late callback after unload. This is invoked via
            // ParentAccessor.CallAction("Loaded") which can be queued/delayed.
            if (!IsLoaded || _lifecycleState != EditorLifecycleState.Loading)
            {
                _desktopBootstrapInFlight = false;
                Debug.WriteLine($"CodeEditorLoaded: ignoring (IsLoaded={IsLoaded}, state={_lifecycleState})");
                return;
            }

            _view = _view ?? throw new InvalidOperationException("The view not set");

            // Enable script execution before init-time calls. SendScriptAsync and
            // InvokeScriptAsync are gated by _initialized, so we must set it before
            // applying initial properties.
            _initialized = true;

            // Emit canonical init-complete marker for diagnostics (always visible).
            Debug.WriteLine("INIT_COMPLETE");

            // Layout first to ensure the editor dimensions are correct.
            await SendScriptAsync("EditorContext.getEditorForElement(element).editor.layout();");

            // Apply all current property values in the correct order
            // This ensures properties set before IsEditorLoaded=true take effect
            await ApplyInitialPropertyValues();

            // Use lifecycle state machine for exactly-once semantics.
            // Transition BEFORE focus to prevent focus ping-pong during init.
            TransitionLifecycle(EditorLifecycleState.Loaded);
            _desktopBootstrapInFlight = false;

            // Defer focus until after init is fully complete to avoid focus ping-pong.
            // Only focus if this CodeEditor is the currently focused element.
#pragma warning disable CS0618 // Type or member is obsolete
            if (FocusManager.GetFocusedElement() == this)
            {
                await SendScriptAsync("EditorContext.getEditorForElement(element).editor.focus();");
                _view.Focus(FocusState.Programmatic);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            Debug.WriteLine("CodeEditorLoaded: complete");
        }

        /// <summary>
        /// Applies all current property values to Monaco in the correct order.
        /// Called during initialization to ensure properties set before IsEditorLoaded=true take effect.
        /// Order matters: language/options must be set before content for proper syntax highlighting.
        /// </summary>
        private async Task ApplyInitialPropertyValues()
        {
            // 1. Apply language and options first (includes ReadOnly, GlyphMargin)
            if (!string.IsNullOrEmpty(CodeLanguage))
            {
                await InvokeScriptAsync("updateLanguage", CodeLanguage);
            }

            // Ensure Options reflect current DP values before sending.
            Options.Language = CodeLanguage;
            Options.ReadOnly = ReadOnly;
            Options.GlyphMargin = HasGlyphMargin;

            await InvokeScriptAsync("updateOptions", Options);

            // 2. Apply theme
            var themeName = RequestedTheme == ElementTheme.Default
                ? _themeListener?.CurrentThemeName ?? "Light"
                : RequestedTheme.ToString();
            var isHighContrast = _themeListener?.IsHighContrast ?? false;
            await InvokeScriptAsync("changeTheme", [themeName, isHighContrast.ToString()]);

            // 3. Apply content after language is configured.
            // Always send values (including empty string) so Monaco state is
            // synchronized on reload -- skipping empty would leave stale content.
            await InvokeScriptAsync("updateContent", Text ?? string.Empty);
            await InvokeScriptAsync("updateSelectedContent", SelectedText ?? string.Empty);

            // 4. Apply decorations and markers last
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

        private void ThemeListener_ThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            if (RequestedTheme == ElementTheme.Default)
            {
                if (!_queue!.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                {
                    await InvokeScriptAsync("changeTheme", args: [e.Listener.CurrentTheme.ToString(), e.Listener.IsHighContrast.ToString()]);
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

        /// <inheritdoc />
        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);

#pragma warning disable CS0618 // Type or member is obsolete
            var presenter = _view;
            if (ShouldForwardPresenterFocus(
                presenter,
                _initialized,
                _lifecycleState,
                FocusManager.GetFocusedElement() == this))
            {
                // Forward Focus onto our inner WebView
                presenter!.Focus(FocusState.Programmatic);
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        internal static bool ShouldInvokeDesktopBootstrap(
            EditorLifecycleState lifecycleState,
            bool initialized,
            bool bootstrapInFlight)
            => lifecycleState == EditorLifecycleState.Loading
                && !initialized
                && !bootstrapInFlight;

        internal static bool ShouldForwardPresenterFocus(
            ICodeEditorPresenter? view,
            bool initialized,
            EditorLifecycleState lifecycleState,
            bool hostIsFocused)
            => view != null
                && initialized
                && lifecycleState == EditorLifecycleState.Loaded
                && hostIsFocused;

        internal static bool ShouldStartDesktopLaunchOnControlLoaded(
            bool isCoreWebView2Initialized,
            bool isLaunchInProgress,
            EditorLifecycleState lifecycleState)
            => !isCoreWebView2Initialized
                && !isLaunchInProgress
                && lifecycleState == EditorLifecycleState.Unloaded;
    }
}
