using System.Diagnostics;
using System.IO;
using System.Text.Json;

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

        public string ElementId => "desktop-" + GetHashCode().ToString("X8");

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

        public bool TriggerKeyDown(WebKeyEventArgs args)
            => ParentCodeEditor?.TriggerKeyDown(args) ?? false;

        private global::System.Uri? _pendingSource;

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

                await _webView.EnsureCoreWebView2Async();

                // Security hardening
                if (_webView.CoreWebView2 is { Settings: { } settings })
                {
                    settings.AreDefaultScriptDialogsEnabled = false;
                    settings.AreDefaultContextMenusEnabled = false;
                    settings.AreHostObjectsAllowed = false;
                }

                // Wire up WebMessageReceived to route inbound messages
                _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                // Wire up navigation events
                _webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                _webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

                // Block external navigation
                _webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                // Only mark initialized after all setup succeeds
                _isCoreWebView2Initialized = true;

                // Note: JsonRpc bridge is created in CreateBridgeTargets() (called from
                // InitialiseWebObjects), not here. Launch() only initializes CoreWebView2.
                // CreateBridgeTargets() is the single owner of JsonRpc lifecycle.

                Debug.WriteLine("DesktopCodeEditorPresenter: CoreWebView2 initialized with security settings");

                // Apply any buffered Source navigation now that security handlers are attached.
                if (_pendingSource is { } pending)
                {
                    _pendingSource = null;
                    _webView.Source = pending;
                }
            }
            catch (Exception e)
            {
                // Reset state so future Launch() calls can retry
                _isCoreWebView2Initialized = false;
                _pendingSource = null;
                TeardownJsonRpc();
                DetachCoreWebView2Handlers();
                Debug.WriteLine($"DesktopCodeEditorPresenter.Launch error: {e}");

                // Re-throw so callers (CodeEditor) can detect failure and abort lifecycle.
                throw;
            }
        }

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
        public void PostWebMessage(string json)
        {
            if (!_isCoreWebView2Initialized || _webView.CoreWebView2 is null)
            {
                throw new InvalidOperationException("CoreWebView2 is not initialized. Call Launch() first.");
            }

            _webView.CoreWebView2.PostWebMessageAsJson(json);
        }

        /// <summary>
        /// Detaches any CoreWebView2 event handlers that may have been partially attached.
        /// Safe to call even if handlers were never attached.
        /// </summary>
        private void DetachCoreWebView2Handlers()
        {
            if (_webView.CoreWebView2 is { } coreWebView2)
            {
                coreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                coreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                coreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                coreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
            }
        }

        private void CoreWebView2_WebMessageReceived(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
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

            // Windows: virtual host mapping serves content over https with a synthetic host.
            // Enforce exact host + default port.
            if (string.Equals(parsed.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(parsed.Host, AllowedVirtualHost, StringComparison.OrdinalIgnoreCase)
                    && parsed.IsDefaultPort;
            }

            // macOS/Linux: Uno converts virtual host URLs to file:// with empty host.
            // Only allow navigation under the configured content root path.
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

        private void CoreWebView2_NavigationStarting(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs args)
        {
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

        private void CoreWebView2_NavigationCompleted(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
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
            _jsonRpc!.AddLocalRpcTarget(parentAccessor);
            _jsonRpc.AddLocalRpcTarget(themeListener);
            _jsonRpc.AddLocalRpcTarget(keyboardListener);
            _jsonRpc.AddLocalRpcTarget(debugLogger);

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

            // Register the initialization handshake targets directly on the presenter.
            _jsonRpc.AddLocalRpcTarget(new BridgeHandshakeTarget(this));

            _jsonRpc.StartListening();

            Debug.WriteLine("DesktopCodeEditorPresenter: JsonRpc bridge started");
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
        /// </summary>
        internal void NotifyLifecycleUpdate(bool isLoading, bool isLoaded)
        {
            if (isLoading) _loadingCount++;
            if (isLoaded) _loadedCount++;

            if (_jsonRpc is not null)
            {
                _ = _jsonRpc.NotifyAsync("editor/lifecycleUpdate",
                    new LifecycleUpdateParams(_loadingCount, _loadedCount));
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
            public void OnBridgeReady(BridgeReadyParams p)
            {
                if (p.ProtocolVersion != ExpectedProtocolVersion)
                {
                    Debug.WriteLine($"DesktopCodeEditorPresenter: bridge/ready protocol version mismatch (expected={ExpectedProtocolVersion}, got={p.ProtocolVersion})");
                    return;
                }

                Debug.WriteLine("DesktopCodeEditorPresenter: bridge/ready received, JSON-RPC transport established");
            }

            [JsonRpcMethod("editor/ready")]
            public void OnEditorReady(EditorReadyParams p)
            {
                if (p.ProtocolVersion != ExpectedProtocolVersion)
                {
                    Debug.WriteLine($"DesktopCodeEditorPresenter: editor/ready protocol version mismatch (expected={ExpectedProtocolVersion}, got={p.ProtocolVersion})");
                    return;
                }

                Debug.WriteLine("DesktopCodeEditorPresenter: editor/ready received, Monaco editor initialized");
            }
        }
    }
}
