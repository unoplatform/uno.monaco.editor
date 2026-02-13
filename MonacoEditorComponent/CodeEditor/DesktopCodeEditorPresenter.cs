using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Monaco.Bridge;
using Monaco.Helpers;

using StreamJsonRpc;

using Windows.Foundation;

namespace Monaco
{
    /// <summary>
    /// Desktop (Skia) presenter that wraps WebView2 for hosting Monaco Editor.
    /// Hosts the JSON-RPC bridge layer for desktop communication with Monaco.
    /// </summary>
    public sealed class DesktopCodeEditorPresenter : ContentControl, ICodeEditorPresenter
    {
        private readonly WebView2 _webView;
        private bool _isCoreWebView2Initialized;
        private WebView2JsonRpcMessageHandler? _messageHandler;
        private JsonRpc? _jsonRpc;
        private string? _desktopContentRoot;
        private bool _fileFallbackAttempted;

        /// <summary>
        /// Initializes a new instance of the <see cref="DesktopCodeEditorPresenter"/> class.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Thrown when called on a WASM platform.</exception>
        public DesktopCodeEditorPresenter()
        {
            if (OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException("DesktopCodeEditorPresenter cannot be used on WASM.");
            }

            _webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            Content = _webView;

            Debug.WriteLine("DesktopCodeEditorPresenter()");
        }

        /// <inheritdoc />
        public event TypedEventHandler<ICodeEditorPresenter?, PresenterNewWindowRequestedEventArgs?>? NewWindowRequested;

        /// <inheritdoc />
        public event TypedEventHandler<ICodeEditorPresenter?, PresenterNavigationStartingEventArgs?>? NavigationStarting;

        /// <inheritdoc />
        public event TypedEventHandler<ICodeEditorPresenter?, PresenterNavigationCompletedEventArgs?>? NavigationCompleted;

        /// <inheritdoc />
        public event EventHandler<WebViewMessageEventArgs>? MessageReceived;

        /// <inheritdoc />
        public CodeEditor? ParentCodeEditor { get; set; }

        /// <summary>
        /// The underlying WebView2 instance. Exposed for Task 5 to attach
        /// WebView2JsonRpcMessageHandler and JsonRpc instance.
        /// </summary>
        internal WebView2 WebView => _webView;

        /// <summary>
        /// Whether CoreWebView2 has been successfully initialized.
        /// Used by Task 5 to determine if JsonRpc can be attached.
        /// </summary>
        internal bool IsCoreWebView2Initialized => _isCoreWebView2Initialized;

        /// <inheritdoc />
        public string ElementId => "desktop-" + GetHashCode().ToString("X8");

        /// <inheritdoc />
        public bool IsSettingValue
        {
            get => ParentCodeEditor?.IsSettingValue ?? false;
            set
            {
                if (ParentCodeEditor is not null)
                {
                    ParentCodeEditor.IsSettingValue = value;
                }
            }
        }

        /// <inheritdoc />
        public bool TriggerKeyDown(WebKeyEventArgs args)
            => ParentCodeEditor?.TriggerKeyDown(args) ?? false;

        private global::System.Uri? _pendingSource;

        /// <inheritdoc />
        public global::System.Uri Source
        {
            get => _isCoreWebView2Initialized ? _webView.Source : (_pendingSource ?? _webView.Source);
            set
            {
                Debug.WriteLine($"DesktopCodeEditorPresenter.Source = {value}");
                if (!_isCoreWebView2Initialized)
                {
                    // Buffer the URI until Launch() completes and security settings are applied.
                    // Navigating before CoreWebView2 is initialized would bypass the allowlist.
                    _pendingSource = value;
                    return;
                }

                _webView.Source = value;
            }
        }

        /// <inheritdoc />
        public async Task Launch()
        {
            try
            {
                if (ParentCodeEditor is null)
                {
                    throw new InvalidOperationException("The ParentCodeEditor property must be set");
                }

                // Idempotency guard: skip if already initialized
                if (_isCoreWebView2Initialized)
                {
                    Debug.WriteLine("DesktopCodeEditorPresenter.Launch: already initialized, skipping");
                    return;
                }

                Debug.WriteLine($"DesktopCodeEditorPresenter.Launch({GetHashCode():X8})");

                // Verify WebKitGTK is available before attempting WebView2 initialization on Linux.
                // EnsureCoreWebView2Async may fail silently or throw cryptic errors without it.
                if (OperatingSystem.IsLinux())
                {
                    EnsureWebKitGtkAvailable();
                }

                await _webView.EnsureCoreWebView2Async();

                // Security hardening — CoreWebView2.Settings may not be supported on all
                // Skia backends (e.g., X11/WebKitGTK), so wrap in try/catch.
                try
                {
                    if (_webView.CoreWebView2 is { Settings: { } settings })
                    {
                        settings.AreDefaultScriptDialogsEnabled = false;
                        settings.AreDefaultContextMenusEnabled = false;
                        settings.AreHostObjectsAllowed = false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DesktopCodeEditorPresenter: Security settings not supported ({ex.GetType().Name})");
                }

                // Wire up navigation events using WebView2 CONTROL-level APIs.
                // Uno documents these as implemented on Skia; the CoreWebView2-level
                // equivalents may not fire on all backends (e.g., X11/WebKitGTK).
                _webView.NavigationStarting += WebView2_NavigationStarting;
                _webView.NavigationCompleted += WebView2_NavigationCompleted;

                // WebMessageReceived — use control-level event (documented on Skia).
                _webView.WebMessageReceived += WebView2_WebMessageReceived;

                // Block external navigation (CoreWebView2-only, best effort)
                try
                {
                    _webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DesktopCodeEditorPresenter: NewWindowRequested not supported ({ex.GetType().Name})");
                }

                // Only mark initialized after all setup succeeds
                _isCoreWebView2Initialized = true;

                // Note: JsonRpc bridge is created in CreateBridgeTargets() (called from
                // InitialiseWebObjects), not here. Launch() only initializes CoreWebView2.
                // CreateBridgeTargets() is the single owner of JsonRpc lifecycle.

                Debug.WriteLine("DesktopCodeEditorPresenter: CoreWebView2 initialized");

                // Configure content serving and navigate to editor.html.
                var contentRoot = ResolveDesktopContentPath();
                AllowedFileContentRoot = contentRoot;
                NavigateToEditorPage(contentRoot);
            }
            catch (Exception e)
            {
                // Reset state so future Launch() calls can retry
                _isCoreWebView2Initialized = false;
                _pendingSource = null;
                TeardownJsonRpc();
                DetachEventHandlers();
                Debug.WriteLine($"DesktopCodeEditorPresenter.Launch error: {e}");

                // Re-throw so callers (CodeEditor) can detect failure and abort lifecycle.
                throw;
            }
        }

        /// <summary>
        /// The element ID used by the desktop editor container in editor.html.
        /// All eval scripts that reference <c>element</c> resolve this from the DOM.
        /// </summary>
        private const string EditorContainerId = "editor-container";

        /// <inheritdoc />
        public async Task<string> InvokeScriptAsync(string script)
        {
            if (!_isCoreWebView2Initialized || _webView.CoreWebView2 is null)
            {
                throw new InvalidOperationException("CoreWebView2 is not initialized. Call Launch() first.");
            }

            return await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        /// <inheritdoc />
        public async Task<string> InvokeMethodAsync(string method, string[] serializedArgs)
        {
            if (!_isCoreWebView2Initialized || _webView.CoreWebView2 is null)
            {
                throw new InvalidOperationException("CoreWebView2 is not initialized. Call Launch() first.");
            }

            // Desktop wraps the call to define `element` from the DOM before invoking the function.
            // editor.html contains <div id="editor-container"> which is the Monaco mount point.
            var script = "var element = document.getElementById(\"" + EditorContainerId + "\"); " +
                         method + "(element," + string.Join(",", serializedArgs) + ");";
            return await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        /// <inheritdoc />
        public async Task<string> InvokeScriptWithElementAsync(string script)
        {
            if (!_isCoreWebView2Initialized || _webView.CoreWebView2 is null)
            {
                throw new InvalidOperationException("CoreWebView2 is not initialized. Call Launch() first.");
            }

            // Prepend element definition so raw scripts that reference `element` work on desktop.
            var wrappedScript = "var element = document.getElementById(\"" + EditorContainerId + "\"); " + script;
            return await _webView.CoreWebView2.ExecuteScriptAsync(wrappedScript);
        }

        /// <inheritdoc />
        public Task PostWebMessageAsync(string json)
        {
            if (!_isCoreWebView2Initialized || _webView.CoreWebView2 is null)
            {
                DiagnosticLog("PostWebMessageAsync: CoreWebView2 not initialized");
                throw new InvalidOperationException("CoreWebView2 is not initialized. Call Launch() first.");
            }

            DiagnosticLog($"PostWebMessageAsync: HasThreadAccess={_webView.DispatcherQueue.HasThreadAccess}, len={json.Length}");

            // CoreWebView2.PostWebMessageAsJson must be called on the UI thread.
            // StreamJsonRpc dispatches WriteAsync from a thread-pool thread, so we
            // marshal back to the DispatcherQueue and await completion.
            if (_webView.DispatcherQueue.HasThreadAccess)
            {
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                DiagnosticLog("PostWebMessageAsync: sent (UI thread)");
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_webView.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    _webView.CoreWebView2.PostWebMessageAsJson(json);
                    DiagnosticLog("PostWebMessageAsync: sent (dispatched)");
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    DiagnosticLog($"PostWebMessageAsync: error in dispatch: {ex.Message}");
                    tcs.SetException(ex);
                }
            }))
            {
                DiagnosticLog("PostWebMessageAsync: TryEnqueue failed");
                tcs.SetException(new InvalidOperationException(
                    "Failed to enqueue PostWebMessageAsJson on the DispatcherQueue."));
            }

            return tcs.Task;
        }

        /// <summary>
        /// Detaches event handlers that may have been partially attached during Launch().
        /// Covers both WebView2 control-level and CoreWebView2-level handlers.
        /// Safe to call even if handlers were never attached.
        /// </summary>
        private void DetachEventHandlers()
        {
            // WebView2 control-level events (always safe to detach)
            _webView.NavigationStarting -= WebView2_NavigationStarting;
            _webView.NavigationCompleted -= WebView2_NavigationCompleted;
            _webView.WebMessageReceived -= WebView2_WebMessageReceived;

            // CoreWebView2-level events (may not have been attached if not supported)
            if (_webView.CoreWebView2 is { } coreWebView2)
            {
                try { coreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested; }
                catch { /* Not supported on this platform */ }
            }
        }

        private void WebView2_WebMessageReceived(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
        {
            // Use WebMessageAsJson to handle both string and object payloads.
            // TryGetWebMessageAsString() would fail for JSON object messages
            // which is the primary format for JSON-RPC bridge traffic.
            var json = args.WebMessageAsJson;
            if (!string.IsNullOrEmpty(json))
            {
                MessageReceived?.Invoke(this, new WebViewMessageEventArgs { MessageJson = json });
            }
        }

        /// <summary>
        /// The allowed virtual host name for content served via SetVirtualHostNameToFolderMapping.
        /// Task 3 will configure the actual mapping; this constant ensures navigation is restricted.
        /// </summary>
        internal const string AllowedVirtualHost = "uno-monaco.example";

        /// <summary>
        /// The allowed local content root path for file:// navigation on macOS/Linux.
        /// Set by Task 3 when configuring the folder mapping. If null, file:// navigation
        /// is blocked entirely (safe default).
        /// </summary>
        internal string? AllowedFileContentRoot { get; set; }

        internal static bool IsNavigationAllowed(string uri, string? allowedFileContentRoot)
        {
            // Allow only exact about:blank (initial WebView2 state) or about:blank with fragment.
            // Using parsed URI comparison to prevent prefix-based bypass (e.g., about:blankevil).
            if (global::System.Uri.TryCreate(uri, UriKind.Absolute, out var aboutCheck)
                && string.Equals(aboutCheck.Scheme, "about", StringComparison.OrdinalIgnoreCase)
                && string.Equals(aboutCheck.AbsolutePath, "blank", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!global::System.Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            {
                return false;
            }

            // Virtual host mapping: Windows WebView2 serves via https://, Uno's
            // cross-platform implementation uses http://. Allow both schemes.
            if (string.Equals(parsed.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsed.Scheme, "http", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(parsed.Host, AllowedVirtualHost, StringComparison.OrdinalIgnoreCase)
                    && parsed.IsDefaultPort;
            }

            // Fallback: file:// navigation for platforms where virtual host mapping
            // resolves to local file paths. Only allow under the configured content root.
            if (string.Equals(parsed.Scheme, "file", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(allowedFileContentRoot))
                {
                    // No content root configured -- block all file navigation.
                    // Task 3 sets this when folder mapping is established.
                    return false;
                }

                // Canonicalize paths to prevent traversal attacks and normalize separators.
                // Use case-insensitive comparison only on Windows (NTFS is case-insensitive).
                // macOS (APFS) and Linux (ext4) default to case-sensitive.
                var localPath = Path.GetFullPath(parsed.LocalPath);
                var canonicalRoot = Path.GetFullPath(allowedFileContentRoot);
                if (!canonicalRoot.EndsWith(Path.DirectorySeparatorChar))
                {
                    canonicalRoot += Path.DirectorySeparatorChar;
                }

                var comparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                return localPath.StartsWith(canonicalRoot, comparison);
            }

            return false;
        }

        private void WebView2_NavigationStarting(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs args)
        {
            Debug.WriteLine($"DesktopCodeEditorPresenter: NavigationStarting → {args.Uri}");

            if (args.Uri is string uri && !IsNavigationAllowed(uri, AllowedFileContentRoot))
            {
                Debug.WriteLine($"DesktopCodeEditorPresenter: Blocked navigation to {uri}");
                args.Cancel = true;
                return;
            }

            var presenterArgs = new PresenterNavigationStartingEventArgs
            {
                Uri = args.Uri is string navUri && global::System.Uri.TryCreate(navUri, UriKind.Absolute, out var parsed) ? parsed : null
            };
            NavigationStarting?.Invoke(this, presenterArgs);

            // Propagate cancel back to the WebView2 args
            if (presenterArgs.Cancel)
            {
                args.Cancel = true;
            }
        }

        private void WebView2_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            Debug.WriteLine($"DesktopCodeEditorPresenter: NavigationCompleted (IsSuccess={args.IsSuccess})");

            if (ShouldFallbackToFileNavigation(args.IsSuccess, _webView.Source, _fileFallbackAttempted, AllowedFileContentRoot)
                && _desktopContentRoot is { } contentRoot)
            {
                _fileFallbackAttempted = true;
                var fallbackUri = BuildFileEditorUri(contentRoot);
                Debug.WriteLine($"DesktopCodeEditorPresenter: Virtual host navigation failed, retrying with {fallbackUri}");
                _webView.Source = fallbackUri;
                return;
            }

            NavigationCompleted?.Invoke(this, new PresenterNavigationCompletedEventArgs
            {
                IsSuccess = args.IsSuccess
            });
        }

        private void CoreWebView2_NewWindowRequested(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs args)
        {
            // Block external navigation in WebView2
            args.Handled = true;

            // Map WebView2 args into portable type so CodeEditor receives the URI
            var presenterArgs = new PresenterNewWindowRequestedEventArgs
            {
                Uri = global::System.Uri.TryCreate(args.Uri, UriKind.Absolute, out var parsed) ? parsed : null
            };
            NewWindowRequested?.Invoke(this, presenterArgs);
        }

        // ============================================================
        // Content serving
        // ============================================================

        /// <summary>
        /// Resolves the DesktopContent folder path from the application's base directory.
        /// Content files are copied to output via CopyToOutputDirectory="PreserveNewest".
        /// </summary>
        private static string ResolveDesktopContentPath()
        {
            var contentRoot = Path.Combine(AppContext.BaseDirectory, "DesktopContent");
            if (!Directory.Exists(contentRoot))
            {
                throw new DirectoryNotFoundException(
                    $"DesktopContent folder not found at {contentRoot}. " +
                    "Ensure the MonacoEditorComponent NuGet package content files are present.");
            }

            return contentRoot;
        }

        /// <summary>
        /// Verifies that WebKitGTK runtime libraries are available on Linux.
        /// Uno Platform's WebView2 on X11 requires libwebkit2gtk (4.1 or 4.0).
        /// Throws with actionable install instructions if the library is not found.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Thrown when WebKitGTK is not installed.</exception>
        private static void EnsureWebKitGtkAvailable()
        {
            // Try 4.1 first (Ubuntu 24.04+), then fall back to 4.0 (Ubuntu 22.04)
            if (NativeLibrary.TryLoad("libwebkit2gtk-4.1.so.0", typeof(DesktopCodeEditorPresenter).Assembly, null, out var handle1))
            {
                NativeLibrary.Free(handle1);
                return;
            }

            if (NativeLibrary.TryLoad("libwebkit2gtk-4.0.so.37", typeof(DesktopCodeEditorPresenter).Assembly, null, out var handle2))
            {
                NativeLibrary.Free(handle2);
                return;
            }

            throw new PlatformNotSupportedException(
                "WebKitGTK runtime library not found. Monaco Editor requires WebKitGTK on Linux.\n\n" +
                "Install it with:\n" +
                "  Ubuntu 24.04+: sudo apt install libgtk-3-0t64 libwebkit2gtk-4.1-0\n" +
                "  Ubuntu 22.04:  sudo apt install libwebkit2gtk-4.0-37\n\n" +
                "See https://platform.uno/docs/articles/controls/WebView.html#x11-specifics");
        }

        /// <summary>
        /// Configures content serving and navigates the WebView2 to editor.html.
        /// Uses <c>WebView2.Source</c> (documented as implemented on Skia) instead of
        /// <c>CoreWebView2.Navigate()</c> which may not fire events on all backends.
        /// Attempts virtual host mapping first; falls back to <c>file://</c> navigation
        /// which works reliably on all WebView2 implementations.
        /// </summary>
        internal static global::System.Uri BuildVirtualHostEditorUri()
            => new($"http://{AllowedVirtualHost}/editor.html");

        internal static global::System.Uri BuildFileEditorUri(string contentRoot)
            => new(Path.Combine(contentRoot, "editor.html"));

        internal static bool ShouldFallbackToFileNavigation(
            bool isSuccess,
            global::System.Uri? currentSource,
            bool fallbackAttempted,
            string? allowedFileContentRoot)
        {
            if (isSuccess || fallbackAttempted || string.IsNullOrEmpty(allowedFileContentRoot) || currentSource is null)
            {
                return false;
            }

            return (string.Equals(currentSource.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentSource.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                && string.Equals(currentSource.Host, AllowedVirtualHost, StringComparison.OrdinalIgnoreCase)
                && currentSource.IsDefaultPort;
        }

        private void NavigateToEditorPage(string contentRoot)
        {
            _desktopContentRoot = contentRoot;
            _fileFallbackAttempted = false;

            // Try virtual host mapping (CoreWebView2 API, may not be available on X11/WebKitGTK)
            bool virtualHostAvailable = false;
            try
            {
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    AllowedVirtualHost,
                    contentRoot,
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
                virtualHostAvailable = true;
                Debug.WriteLine("DesktopCodeEditorPresenter: Virtual host mapping configured");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DesktopCodeEditorPresenter: Virtual host mapping not available ({ex.GetType().Name}: {ex.Message})");
            }

            // Navigate using WebView2.Source (documented as implemented on Skia).
            // Prefer virtual host (proper origins for CORS); fall back to file://.
            global::System.Uri editorUri;
            if (virtualHostAvailable)
            {
                editorUri = BuildVirtualHostEditorUri();
            }
            else
            {
                // file:// works reliably on all WebView2 backends (Edge, WebKitGTK, WKWebView).
                // editor.html uses classic scripts (no ES modules) specifically for file:// compat.
                _fileFallbackAttempted = true;
                editorUri = BuildFileEditorUri(contentRoot);
            }

            _webView.Source = editorUri;
            Debug.WriteLine($"DesktopCodeEditorPresenter: Navigating to {editorUri} (content root: {contentRoot})");
        }

        // ============================================================
        // JSON-RPC bridge wiring
        // ============================================================

        private const int ExpectedProtocolVersion = 1;

        /// <summary>
        /// The <see cref="JsonRpc"/> instance for desktop bridge communication.
        /// Exposed internally so desktop bridge helpers can send C#-to-JS notifications/requests.
        /// </summary>
        internal JsonRpc? Rpc => _jsonRpc;

        /// <summary>
        /// Creates the bridge targets and returns them for registration on CodeEditor.
        /// Called from <see cref="CodeEditor.InitialiseWebObjects"/> on the desktop path.
        /// Recreates the JsonRpc instance to ensure a clean target registration --
        /// prevents stale target accumulation across unload/reload cycles.
        /// </summary>
        internal (IParentAccessor ParentAccessor, IThemeListener ThemeListener, IKeyboardListener KeyboardListener, IDebugLogger DebugLogger)
            CreateBridgeTargets(DispatcherQueue queue)
        {
            // Tear down and recreate JsonRpc to prevent stale target accumulation.
            // Each call to CreateBridgeTargets starts with a fresh JsonRpc instance
            // so AddLocalRpcTarget registrations never duplicate across re-init cycles.
            TeardownJsonRpc();
            SetupJsonRpc();

            var parentAccessor = new ParentAccessorDesktop(this, queue);
            var themeListener = new ThemeListenerDesktop(queue);
            var keyboardListener = new KeyboardListenerDesktop(this);
            var debugLogger = new DebugLoggerDesktop();

            // Register as local RPC targets so StreamJsonRpc routes messages automatically.
            // All targets must be registered BEFORE StartListening -- StreamJsonRpc locks
            // the configuration once listening begins and AddLocalRpcTarget will throw.
            _jsonRpc!.AddLocalRpcTarget(parentAccessor);
            _jsonRpc.AddLocalRpcTarget(themeListener);
            _jsonRpc.AddLocalRpcTarget(keyboardListener);
            _jsonRpc.AddLocalRpcTarget(debugLogger);

            _jsonRpc.StartListening();
            Debug.WriteLine("DesktopCodeEditorPresenter: JsonRpc bridge started");

            return (parentAccessor, themeListener, keyboardListener, debugLogger);
        }

        private void SetupJsonRpc()
        {
            var formatter = new SystemTextJsonFormatter
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    TypeInfoResolverChain = { BridgeSerializerContext.Default },
                },
            };

            _messageHandler = new WebView2JsonRpcMessageHandler(this, formatter);
            _jsonRpc = new JsonRpc(_messageHandler);

            // Set the SynchronizationContext so all RPC method handlers run on the
            // UI thread directly. This eliminates the need for _queue.EnqueueAsync
            // in every handler (getJsonValue, callAction, setValue, etc.).
            // CreateBridgeTargets is called from InitialiseWebObjects which runs on
            // the UI thread, so SynchronizationContext.Current is the UI context.
            _jsonRpc.SynchronizationContext = SynchronizationContext.Current;
            DiagnosticLog($"DesktopCodeEditorPresenter: JsonRpc.SynchronizationContext set (non-null={SynchronizationContext.Current is not null})");

            // Register the initialization handshake targets directly on the presenter.
            // StartListening is deferred to CreateBridgeTargets after all targets are registered.
            _jsonRpc.AddLocalRpcTarget(new BridgeHandshakeTarget(this));
        }

        private void TeardownJsonRpc()
        {
            if (_jsonRpc is not null)
            {
                _jsonRpc.Dispose();
                _jsonRpc = null;
            }

            if (_messageHandler is not null)
            {
                _messageHandler.Dispose();
                _messageHandler = null;
            }
        }

        // Lifecycle event counters for testability (consumed by Task 8 Playwright tests).
        private int _loadingCount;
        private int _loadedCount;

        /// <summary>
        /// Emits an editor/lifecycleUpdate notification via JSON-RPC with current
        /// EditorLoading/EditorLoaded counts. Called by <see cref="CodeEditor"/>
        /// when lifecycle events fire.
        /// Gated on CoreWebView2 initialization -- early lifecycle transitions
        /// (e.g., Loading before Launch()) are counted but not sent until the
        /// transport is ready. The Loaded transition fires after Launch() completes.
        /// </summary>
        internal void NotifyLifecycleUpdate(bool isLoading, bool isLoaded)
        {
            if (isLoading) _loadingCount++;
            if (isLoaded) _loadedCount++;

            // Only send if CoreWebView2 is initialized and JsonRpc is active.
            // PostWebMessage throws before CoreWebView2 init, so we must gate here.
            // Early counts (Loading) are preserved and sent with the next notification
            // (Loaded) which fires after Launch() completes.
            if (_isCoreWebView2Initialized && _jsonRpc is not null)
            {
                _ = _jsonRpc.NotifyAsync("editor/lifecycleUpdate",
                    new LifecycleUpdateParams(_loadingCount, _loadedCount));
            }
        }

        /// <summary>
        /// Writes a diagnostic message to stdout when the <c>MONACO_DIAGNOSTICS</c>
        /// environment variable is set to <c>"1"</c>. Used for Release-testable
        /// diagnostics that do not appear in production output.
        /// </summary>
        internal static void DiagnosticLog(string message)
        {
            if (Environment.GetEnvironmentVariable("MONACO_DIAGNOSTICS") == "1")
            {
                Console.WriteLine(message);
            }
        }

        /// <summary>
        /// JSON-RPC target for bridge/ready and editor/ready handshake notifications.
        /// </summary>
        private sealed class BridgeHandshakeTarget
        {
            private readonly DesktopCodeEditorPresenter _presenter;

            public BridgeHandshakeTarget(DesktopCodeEditorPresenter presenter) => _presenter = presenter;

            [JsonRpcMethod("bridge/ready")]
            public void OnBridgeReady(int protocolVersion)
            {
                if (protocolVersion != ExpectedProtocolVersion)
                {
                    DiagnosticLog($"DesktopCodeEditorPresenter: bridge/ready protocol version mismatch (expected={ExpectedProtocolVersion}, got={protocolVersion})");
                    return;
                }

                DiagnosticLog("DesktopCodeEditorPresenter: bridge/ready received, JSON-RPC transport established");
            }

            [JsonRpcMethod("editor/ready")]
            public void OnEditorReady(int protocolVersion)
            {
                if (protocolVersion != ExpectedProtocolVersion)
                {
                    DiagnosticLog($"DesktopCodeEditorPresenter: editor/ready protocol version mismatch (expected={ExpectedProtocolVersion}, got={protocolVersion})");
                    return;
                }

                DiagnosticLog("DesktopCodeEditorPresenter: editor/ready received, Monaco editor initialized");
            }
        }
    }
}
