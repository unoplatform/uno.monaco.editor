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
    public abstract partial class EditorHostBase
    {
        // Override default Loaded/Loading event so we can make sure we've initialized our WebView contents with the EditorHostBase.

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
        public event TypedEventHandler<EditorHostBase, OpenLinkRequestedEventArgs>? OpenLinkRequested;

        /// <summary>
        /// Occurs when an internal exception is encountered while executing a script command.
        /// Subscribe to this event for diagnostics and error reporting.
        /// </summary>
        public event TypedEventHandler<EditorHostBase, Exception>? InternalException;

        /// <summary>
        /// Occurs when a key is pressed inside the Monaco editor.
        /// Shadows <see cref="Microsoft.UI.Xaml.UIElement.KeyDown"/> to provide
        /// Monaco-specific key event arguments.
        /// </summary>
        public new event WebKeyEventHandler? KeyDown;

        private IThemeListener? _themeListener;
        private bool _codeEditorLoadedInFlight;
        private EditorLifecycleState _lifecycleState = EditorLifecycleState.Unloaded;
        private int _desktopInitTimeoutRetryCount;

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
                    _desktopInitTimeoutRetryCount = 0;
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
                    _desktopInitTimeoutRetryCount = 0;
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
                if (_view is DesktopCodeEditorPresenter
                    && args is { IsSuccess: true }
                    && ShouldDeferDesktopBootstrapOnNavigationCompleted(
                        IsLoaded,
                        navigationSucceeded: true,
                        canInvokeBootstrap: ShouldInvokeDesktopBootstrap(_lifecycleState, _initialized, _desktopBootstrapInFlight)))
                {
                    _pendingDesktopBootstrapAfterLoad = true;
                    DesktopCodeEditorPresenter.DiagnosticLog(
                        "WebView_NavigationCompleted: control not loaded, deferring desktop bootstrap until reload.");
                }

                DesktopCodeEditorPresenter.DiagnosticLog("WebView_NavigationCompleted: control not loaded, ignoring.");
                return;
            }

            // Gate on navigation success. A failed or blocked navigation should not
            // advance the lifecycle to Loaded.
            if (args is { IsSuccess: false })
            {
                _pendingDesktopBootstrapAfterLoad = false;
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
                _pendingDesktopBootstrapAfterLoad = false;
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
        protected virtual string BuildInitialStateJson()
        {
            // Use FallbackOptions (reflection-based) since the map holds loose object values
            // rather than a registered type. Safe on desktop (native code, not AOT-WASM).
            var json = JsonSerializer.Serialize(BuildInitialStateMap(), MonacoJsonContext.FallbackOptions);

            DesktopCodeEditorPresenter.DiagnosticLog($"BuildInitialStateJson: {json}");
            return json;
        }

        /// <summary>
        /// Builds the initial-state values that <see cref="BuildInitialStateJson"/> serializes.
        /// Derived controls override this to contribute their own keys rather than rebuilding
        /// the theme resolution.
        /// </summary>
        /// <returns>
        /// The state map. Keys are camelCase because they are consumed verbatim by the
        /// <c>InitialState</c> interface in <c>asyncCallbackHelpers.ts</c> -- the serializer's
        /// camelCase policy applies to properties, not to dictionary keys.
        /// </returns>
        protected virtual Dictionary<string, object?> BuildInitialStateMap()
        {
            var themeName = RequestedTheme == ElementTheme.Default
                ? _themeListener?.CurrentThemeName ?? "Light"
                : RequestedTheme.ToString();

            var state = new Dictionary<string, object?>
            {
                ["requestedTheme"] = (int)RequestedTheme,
                ["themeName"] = themeName,
                ["isHighContrast"] = _themeListener?.IsHighContrast ?? false,
            };

            // A control with no primary document (MultiDiffCodeEditor) has nothing to put here,
            // and the TS side would only push these onto an EditorContext.editor it does not have.
            if (HasPrimaryDocument)
            {
                state["text"] = PrimaryText ?? string.Empty;
                state["language"] = CodeLanguage ?? "plaintext";
                state["readOnly"] = ReadOnly;
            }

            return state;
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

            // Callback-loss recovery: some desktop backends (notably WKWebView host paths)
            // can initialize Monaco successfully but never deliver the JS->C# Loaded callback.
            // Probe the JS init-complete flag and synthesize CodeEditorLoaded when observed.
            _ = MonitorDesktopInitCompletionAsync();

            // Timeout fallback: if CodeEditorLoaded never fires (script failure,
            // Monaco crash, etc.), surface a diagnostic error after 30 seconds.
            _ = MonitorInitTimeoutAsync();
        }

        private async Task InvokeDesktopBootstrapAsync(ICodeEditorPresenter presenter, string escapedState, string source)
        {
            try
            {
                await presenter.InvokeScriptAsync(BuildCreateMonacoEditorScript(BootstrapFunctionName, escapedState));
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
            => BuildCreateMonacoEditorScript("createMonacoEditor", escapedState);

        internal static string BuildCreateMonacoEditorScript(string bootstrapFunctionName, string escapedState)
            => $"void {bootstrapFunctionName}(null, 'editor-container', '', {escapedState})";

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

                if (_view is DesktopCodeEditorPresenter desktopPresenter)
                {
                    if (await HasDesktopEditorContextAsync(desktopPresenter))
                    {
                        DesktopCodeEditorPresenter.DiagnosticLog(
                            "INIT_TIMEOUT: editor context exists despite missing callback; synthesizing CodeEditorLoaded.");
                        CodeEditorLoaded();
                        return;
                    }

                    if (_desktopInitTimeoutRetryCount < 1)
                    {
                        _desktopInitTimeoutRetryCount++;
                        var initError = await ReadDesktopInitErrorAsync(desktopPresenter);
                        var runtimeSnapshot = await ReadDesktopRuntimeSnapshotAsync(desktopPresenter);
                        if (await IsDesktopInitCompleteAsync(desktopPresenter, runtimeSnapshot))
                        {
                            DesktopCodeEditorPresenter.DiagnosticLog(
                                $"INIT_TIMEOUT: JS init complete after timeout probe (runtime={runtimeSnapshot ?? "null"}); synthesizing CodeEditorLoaded.");
                            CodeEditorLoaded();
                            return;
                        }

                        DesktopCodeEditorPresenter.DiagnosticLog(
                            $"INIT_TIMEOUT: retrying createMonacoEditor (attempt={_desktopInitTimeoutRetryCount + 1}, initError={initError ?? "none"}, runtime={runtimeSnapshot ?? "null"}).");
                        RebootstrapMonacoAsync();
                        return;
                    }
                }

                var msg = $"Monaco editor initialization timed out after {timeoutMs}ms. " +
                    "CodeEditorLoaded callback was never received. Check browser console for errors.";
                DesktopCodeEditorPresenter.DiagnosticLog($"INIT_TIMEOUT: {msg}");
                InternalException?.Invoke(this, new TimeoutException(msg));
            }
        }

        private async Task MonitorDesktopInitCompletionAsync()
        {
            if (_view is not DesktopCodeEditorPresenter desktopPresenter)
            {
                return;
            }

            const int probeIntervalMs = 250;
            const int maxProbeAttempts = 40; // 10 seconds total
            for (var attempt = 0; attempt < maxProbeAttempts; attempt++)
            {
                if (_lifecycleState != EditorLifecycleState.Loading || !_desktopBootstrapInFlight)
                {
                    return;
                }

                if (await IsDesktopInitCompleteAsync(desktopPresenter))
                {
                    DesktopCodeEditorPresenter.DiagnosticLog(
                        "INIT_COMPLETE_PROBE: JS init complete without callback; synthesizing CodeEditorLoaded.");
                    CodeEditorLoaded();
                    return;
                }

                await Task.Delay(probeIntervalMs);
            }
        }

        private static bool ScriptResultIsTrue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().Trim('"');
            return string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> HasDesktopEditorContextAsync(DesktopCodeEditorPresenter desktopPresenter)
        {
            try
            {
                var hasContext = await desktopPresenter.InvokeScriptAsync("""
                    (() => {
                        const element = document.getElementById('editor-container');
                        const getContext = typeof EditorContext !== 'undefined'
                            ? (EditorContext.tryGetEditorForElement || EditorContext.getEditorForElement)
                            : null;
                        const context = getContext ? getContext.call(EditorContext, element) : null;
                        return !!(context && context.editor && context.model);
                    })()
                    """);

                return ScriptResultIsTrue(hasContext);
            }
            catch (Exception ex)
            {
                DesktopCodeEditorPresenter.DiagnosticLog($"INIT_TIMEOUT: context probe failed: {ex.Message}");
                return false;
            }
        }

        private async Task<string?> ReadDesktopInitErrorAsync(DesktopCodeEditorPresenter desktopPresenter)
        {
            try
            {
                var result = await desktopPresenter.InvokeScriptAsync("globalThis.__unoMonacoInitError ?? null");
                if (string.Equals(result, "null", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"probe-failed:{ex.Message}";
            }
        }

        private async Task<string?> ReadDesktopRuntimeSnapshotAsync(DesktopCodeEditorPresenter desktopPresenter)
        {
            try
            {
                return await desktopPresenter.InvokeScriptAsync("""
                    (() => JSON.stringify({
                        isDesktopHostDetected: !!(globalThis.isDesktopHost && globalThis.isDesktopHost()),
                        hasChromeWebView: !!(globalThis.chrome && globalThis.chrome.webview && globalThis.chrome.webview.postMessage),
                        hasWebkitBridge: !!(globalThis.webkit && globalThis.webkit.messageHandlers && globalThis.webkit.messageHandlers.unoWebView && globalThis.webkit.messageHandlers.unoWebView.postMessage),
                        hasWindowModule: typeof window.Module !== 'undefined',
                        hasJsonRpc: !!globalThis.__jsonRpc,
                        initComplete: globalThis.__unoMonacoInitComplete ?? null
                    }))()
                    """);
            }
            catch (Exception ex)
            {
                return $"runtime-probe-failed:{ex.Message}";
            }
        }

        private async Task<bool> IsDesktopInitCompleteAsync(DesktopCodeEditorPresenter desktopPresenter, string? runtimeSnapshot = null)
        {
            if (RuntimeSnapshotIndicatesInitComplete(runtimeSnapshot))
            {
                return true;
            }

            var snapshot = runtimeSnapshot ?? await ReadDesktopRuntimeSnapshotAsync(desktopPresenter);
            if (RuntimeSnapshotIndicatesInitComplete(snapshot))
            {
                return true;
            }

            try
            {
                var result = await desktopPresenter.InvokeScriptAsync("globalThis.__unoMonacoInitComplete === true");
                return ScriptResultIsTrue(result);
            }
            catch
            {
                return false;
            }
        }

        internal static bool RuntimeSnapshotIndicatesInitComplete(string? runtimeSnapshot)
        {
            if (string.IsNullOrWhiteSpace(runtimeSnapshot))
            {
                return false;
            }

            if (TryReadInitCompleteFlag(runtimeSnapshot, out var isComplete))
            {
                return isComplete;
            }

            try
            {
                var decoded = JsonSerializer.Deserialize<string>(runtimeSnapshot);
                if (TryReadInitCompleteFlag(decoded, out isComplete))
                {
                    return isComplete;
                }
            }
            catch (JsonException)
            {
            }

            return runtimeSnapshot.Contains("\"initComplete\":true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryReadInitCompleteFlag(string? runtimeSnapshot, out bool isComplete)
        {
            isComplete = false;
            if (string.IsNullOrWhiteSpace(runtimeSnapshot))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(runtimeSnapshot);
                if (doc.RootElement.ValueKind != JsonValueKind.Object
                    || !doc.RootElement.TryGetProperty("initComplete", out var initCompleteElement))
                {
                    return false;
                }

                switch (initCompleteElement.ValueKind)
                {
                    case JsonValueKind.True:
                        isComplete = true;
                        return true;
                    case JsonValueKind.False:
                        isComplete = false;
                        return true;
                    case JsonValueKind.String:
                        return bool.TryParse(initCompleteElement.GetString(), out isComplete);
                    case JsonValueKind.Number when initCompleteElement.TryGetInt32(out var numericValue):
                        isComplete = numericValue != 0;
                        return true;
                    default:
                        return false;
                }
            }
            catch (JsonException)
            {
                return false;
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
            _pendingDesktopBootstrapAfterLoad = false;

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

                RegisterBridgeCallbacks(_parentAccessor);

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
            Debug.WriteLine($"CodeEditorLoaded: IsLoaded={IsLoaded}, state={_lifecycleState}, bootstrapInFlight={_desktopBootstrapInFlight}, HasThreadAccess={_queue?.HasThreadAccess}");

            // Guard against late callback after unload. This is invoked via
            // ParentAccessor.CallAction("Loaded") which can be queued/delayed.
            if (!ShouldProcessCodeEditorLoaded(IsLoaded, _lifecycleState, _desktopBootstrapInFlight))
            {
                _desktopBootstrapInFlight = false;
                Debug.WriteLine($"CodeEditorLoaded: ignoring (IsLoaded={IsLoaded}, state={_lifecycleState}, bootstrapInFlight={_desktopBootstrapInFlight})");
                return;
            }

            // Re-entrancy guard. Both the "Loaded" bridge callback and
            // MonitorDesktopInitCompletionAsync's probe can arrive for the same bootstrap, and
            // the state this method transitions on is only updated after two awaits -- so a
            // probe landing inside that window still passes the guard above and runs the whole
            // body a second time, re-applying every initial property and potentially raising
            // EditorLoaded twice for one initialization cycle.
            if (_codeEditorLoadedInFlight)
            {
                Debug.WriteLine("CodeEditorLoaded: ignoring (already in flight for this bootstrap)");
                return;
            }

            _codeEditorLoadedInFlight = true;
            try
            {
                _view = _view ?? throw new InvalidOperationException("The view not set");

                // Enable script execution before init-time calls. SendScriptAsync and
                // InvokeScriptAsync are gated by _initialized, so we must set it before
                // applying initial properties.
                _initialized = true;

                // Emit canonical init-complete marker for diagnostics.
                //
                // Through DiagnosticLog, not Debug.WriteLine: Debug.WriteLine is
                // [Conditional("DEBUG")] and so compiles out of the Release builds the desktop
                // integration suite runs against, where this marker is the evidence that
                // CodeEditorLoaded ran. Qualified by control type so a host with more than one
                // editor can tell which one initialized, and so the marker cannot be confused
                // with INIT_COMPLETE_PROBE on a substring match.
                DesktopCodeEditorPresenter.DiagnosticLog($"INIT_COMPLETE:{GetType().Name}");

                // Layout first to ensure the editor dimensions are correct.
                await SendScriptAsync("layoutEditor(element);");

                // Apply all current property values in the correct order
                // This ensures properties set before IsEditorLoaded=true take effect
                await ApplyInitialPropertyValues();

                // Transition to Loaded only when coming from Loading.
                if (_lifecycleState == EditorLifecycleState.Loading)
                {
                    TransitionLifecycle(EditorLifecycleState.Loaded);
                }
                else
                {
                    IsEditorLoaded = true;
                    if (_desktopBootstrapInFlight && _lifecycleState == EditorLifecycleState.Loaded)
                    {
                        EditorLoaded?.Invoke(this, new RoutedEventArgs());
                    }
                }

                _desktopBootstrapInFlight = false;
                _pendingDesktopBootstrapAfterLoad = false;

                // Defer focus until after init is fully complete to avoid focus ping-pong.
                // Only focus if this EditorHostBase is the currently focused element.
#pragma warning disable CS0618 // Type or member is obsolete
                if (FocusManager.GetFocusedElement() == this)
                {
                    await SendScriptAsync("EditorContext.getEditorForElement(element).editor.focus();");
                    _view.Focus(FocusState.Programmatic);
                }
#pragma warning restore CS0618 // Type or member is obsolete

                Debug.WriteLine("CodeEditorLoaded: complete");
            }
            catch (Exception ex)
            {
                _desktopBootstrapInFlight = false;
                Debug.WriteLine($"CodeEditorLoaded failed: {ex}");
                InternalException?.Invoke(this, ex);
            }
            finally
            {
                _codeEditorLoadedInFlight = false;
            }
        }

        /// <summary>
        /// Applies all current property values to Monaco in the correct order.
        /// Called during initialization to ensure properties set before IsEditorLoaded=true take effect.
        /// Order matters: language/options must be set before content for proper syntax highlighting.
        /// </summary>
        protected virtual async Task ApplyInitialPropertyValues()
        {
            // 1. Apply language and options first (includes ReadOnly, GlyphMargin)
            if (HasPrimaryDocument)
            {
                if (!string.IsNullOrEmpty(CodeLanguage))
                {
                    await InvokeScriptAsync("updateLanguage", CodeLanguage);
                }

                // Ensure Options reflect current DP values before sending.
                Options.Language = CodeLanguage;
                Options.ReadOnly = ReadOnly;
                Options.GlyphMargin = HasGlyphMargin;

                await InvokeScriptAsync("updateOptions", Options);
            }

            // 2. Apply theme
            var themeName = RequestedTheme == ElementTheme.Default
                ? _themeListener?.CurrentThemeName ?? "Light"
                : RequestedTheme.ToString();
            var isHighContrast = _themeListener?.IsHighContrast ?? false;
            await InvokeScriptAsync("changeTheme", [themeName, isHighContrast.ToString()]);

            // Everything below targets EditorContext.editor. A multi-file diff element has no such
            // editor -- its per-file editors are pooled and recycled -- so the whole single-document
            // tail is skipped and the derived control pushes its own file list instead.
            if (!HasPrimaryDocument)
            {
                return;
            }

            // 3. Apply content after language is configured.
            // Always send values (including empty string) so Monaco state is
            // synchronized on reload -- skipping empty would leave stale content.
            await InvokeScriptAsync("updateContent", PrimaryText ?? string.Empty);
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
            if (obj is EditorHostBase editor
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

        internal static bool ShouldRestoreDesktopBridgeOnControlLoaded(
            EditorLifecycleState lifecycleState,
            bool hasInitializedPresenter,
            bool isCoreWebView2Initialized,
            bool isLaunchInProgress,
            bool bootstrapInFlight)
            => lifecycleState == EditorLifecycleState.Unloaded
                && !hasInitializedPresenter
                && isCoreWebView2Initialized
                && !isLaunchInProgress
                && !bootstrapInFlight;

        internal static bool ShouldDeferDesktopBootstrapOnNavigationCompleted(
            bool controlIsLoaded,
            bool navigationSucceeded,
            bool canInvokeBootstrap)
            => !controlIsLoaded
                && navigationSucceeded
                && canInvokeBootstrap;

        internal static bool ShouldResumeDeferredDesktopBootstrapOnControlLoaded(
            bool hasPendingBootstrap,
            bool isCoreWebView2Initialized,
            bool isLaunchInProgress,
            bool canInvokeBootstrap)
            => hasPendingBootstrap
                && isCoreWebView2Initialized
                && !isLaunchInProgress
                && canInvokeBootstrap;

        internal static bool ShouldProcessCodeEditorLoaded(
            bool isLoaded,
            EditorLifecycleState lifecycleState,
            bool bootstrapInFlight)
            => isLoaded
                && (lifecycleState == EditorLifecycleState.Loading
                    || (bootstrapInFlight && lifecycleState == EditorLifecycleState.Loaded));
    }
}
