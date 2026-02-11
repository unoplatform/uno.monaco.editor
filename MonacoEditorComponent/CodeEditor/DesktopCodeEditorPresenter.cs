using System.Diagnostics;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Monaco.Helpers;

using Windows.Foundation;

namespace Monaco
{
    /// <summary>
    /// Desktop (Skia) presenter that wraps WebView2 for hosting Monaco Editor.
    /// Shell implementation -- full lifecycle wiring in Task 5.
    /// </summary>
    public sealed class DesktopCodeEditorPresenter : ContentControl, ICodeEditorPresenter
    {
        private readonly WebView2 _webView;
        private bool _isCoreWebView2Initialized;

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
        public event TypedEventHandler<ICodeEditorPresenter?, WebViewNewWindowRequestedEventArgs?>? NewWindowRequested;

        /// <inheritdoc />
        public event TypedEventHandler<ICodeEditorPresenter?, WebViewNavigationStartingEventArgs?>? NavigationStarting;

        /// <inheritdoc />
        public event TypedEventHandler<ICodeEditorPresenter?, WebViewNavigationCompletedEventArgs?>? NavigationCompleted;

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

        public global::System.Uri Source
        {
            get => _webView.Source;
            set
            {
                Debug.WriteLine($"DesktopCodeEditorPresenter.Source = {value}");
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
                _isCoreWebView2Initialized = true;

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

                Debug.WriteLine("DesktopCodeEditorPresenter: CoreWebView2 initialized with security settings");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"DesktopCodeEditorPresenter.Launch error: {e}");
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
        private const string AllowedVirtualHost = "uno-monaco.example";

        private void CoreWebView2_NavigationStarting(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs args)
        {
            // Block navigation to untrusted origins.
            // Allow about:blank (initial state), data: URIs, and the virtual host.
            if (args.Uri is string uri
                && !uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                && !uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                && !uri.Contains(AllowedVirtualHost, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"DesktopCodeEditorPresenter: Blocked navigation to {uri}");
                args.Cancel = true;
                return;
            }

            NavigationStarting?.Invoke(this, null);
        }

        private void CoreWebView2_NavigationCompleted(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            // WebViewNavigationCompletedEventArgs cannot be directly constructed.
            // Pass null and let the handler check IsSuccess through the presenter state.
            NavigationCompleted?.Invoke(this, null);
        }

        private void CoreWebView2_NewWindowRequested(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs args)
        {
            // Block external navigation
            args.Handled = true;
            NewWindowRequested?.Invoke(this, null);
        }
    }
}
