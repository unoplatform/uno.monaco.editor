using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

using Monaco.Helpers;

using Uno.Extensions;
using Uno.Logging;
using Uno.UI.NativeElementHosting;

using Windows.Foundation;

namespace Monaco
{
    /// <summary>
    /// WebAssembly presenter that hosts the Monaco Editor inside a <c>BrowserHtmlElement</c>.
    /// Uses JSImport/JSExport for direct interop with the browser DOM.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown if instantiated on a non-WASM platform.
    /// </exception>
    public partial class WasmCodeEditorPresenter : ContentControl, ICodeEditorPresenter
    {
        private static readonly string UNO_BOOTSTRAP_APP_BASE = global::System.Environment.GetEnvironmentVariable(nameof(UNO_BOOTSTRAP_APP_BASE)) ?? "";
        private static readonly string UNO_BOOTSTRAP_WEBAPP_BASE_PATH = Environment.GetEnvironmentVariable(nameof(UNO_BOOTSTRAP_WEBAPP_BASE_PATH)) ?? "";
        private readonly BrowserHtmlElement _element;

        /// <summary>
        /// Initializes a new instance of the <see cref="WasmCodeEditorPresenter"/> class.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Thrown when called on a non-WASM platform.</exception>
        public WasmCodeEditorPresenter()
        {
            if (!OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException("WasmCodeEditorPresenter can only be used on WASM.");
            }

            Debug.WriteLine("WasmCodeEditorPresenter()");
            Content = _element = BrowserHtmlElement.CreateHtmlElement("monaco-" + this.GetHashCode(), "div");
        }

        /// <inheritdoc />
        public string ElementId => _element.ElementId;

        /// <inheritdoc />
        public event TypedEventHandler<ICodeEditorPresenter?, PresenterNewWindowRequestedEventArgs?>? NewWindowRequested; // ignored for now (external navigation)

        /// <inheritdoc />
        public event TypedEventHandler<ICodeEditorPresenter?, PresenterNavigationStartingEventArgs?>? NavigationStarting;

        /// <inheritdoc />
        public event TypedEventHandler<ICodeEditorPresenter?, PresenterNavigationCompletedEventArgs?>? NavigationCompleted; // ignored for now (only focus the editor)

        /// <inheritdoc />
        /// <remarks>WASM presenter never fires this event. JSExport direct calls are used instead.</remarks>
        public event EventHandler<WebViewMessageEventArgs>? MessageReceived;

        /// <inheritdoc />
        public CodeEditor? ParentCodeEditor { get; set; }

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

        /// <inheritdoc />
        public global::System.Uri Source
        {
            get => new(NativeMethods.GetSrc(_element.ElementId));
            set
            {
                string target;
                if (value.IsAbsoluteUri)
                {
                    if (value.Scheme == "file")
                    {
                        // Local files are assumed as coming from the remote server
                        target = UNO_BOOTSTRAP_APP_BASE == null ? value.PathAndQuery : UNO_BOOTSTRAP_WEBAPP_BASE_PATH + UNO_BOOTSTRAP_APP_BASE + value.PathAndQuery;
                    }
                    else
                    {
                        target = value.AbsoluteUri;
                    }
                }
                else
                {
                    target = UNO_BOOTSTRAP_APP_BASE == null
                        ? value.OriginalString
                        : UNO_BOOTSTRAP_WEBAPP_BASE_PATH + UNO_BOOTSTRAP_APP_BASE + "/" + value.OriginalString;
                }

                if (this.Log().IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                {
                    this.Log().Debug($"Loading {target} (Nav is null {NavigationStarting == null})");
                }

                NativeMethods.SetSrc(_element.ElementId, target);

                if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => NavigationStarting?.Invoke(this, null)))
                {
                    Debug.WriteLine("Failed to enqueue NavigationStarting -- dispatcher queue unavailable");
                }
            }
        }

        /// <inheritdoc />
        public async Task Launch()
        {
            try
            {
                if (ParentCodeEditor is null)
                {
                    throw new InvalidOperationException($"The ParentCodeEditor property must be set");
                }

                Debug.WriteLine($"InitializeMonaco({this.GetHashCode():X8})");
                await NativeMethods.InitializeMonaco(this, _element.ElementId, $"{UNO_BOOTSTRAP_WEBAPP_BASE_PATH}{UNO_BOOTSTRAP_APP_BASE}");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"WasmCodeEditorPresenter.Launch error: {e}");
                throw;
            }
        }

        /// <inheritdoc />
        public Task<string> InvokeScriptAsync(string script)
        {
            var result = Extensions.NativeMethods.InvokeJS(_element.ElementId, script);
            return Task.FromResult(result);
        }

        /// <inheritdoc />
        /// <remarks>Not used on WASM. JSExport direct calls are used instead.</remarks>
        public void PostWebMessage(string json)
        {
            throw new PlatformNotSupportedException("PostWebMessage is not supported on WASM. Use JSExport direct calls instead.");
        }

        static partial class NativeMethods
        {
            [JSImport("globalThis.getSrc")]
            public static partial string GetSrc(string elementId);

            [JSImport("globalThis.setSrc")]
            public static partial void SetSrc(string elementId, string src);

            [JSImport("globalThis.createMonacoEditor")]
            public static partial Task InitializeMonaco([JSMarshalAs<JSType.Any>] object managedOwner, string elementId, string baseUri);
        }
    }
}
