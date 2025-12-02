using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;

using Microsoft.UI.Xaml.Controls;

using Monaco.Helpers;

using Uno.Extensions;
using Uno.Logging;
using Uno.UI.NativeElementHosting;

using Windows.Foundation;
using Windows.UI.Core;

namespace Monaco
{
    public partial class CodeEditorPresenter : ContentControl, ICodeEditorPresenter
    {
        private static readonly string UNO_BOOTSTRAP_APP_BASE = global::System.Environment.GetEnvironmentVariable(nameof(UNO_BOOTSTRAP_APP_BASE)) ?? "";
        private static readonly string UNO_BOOTSTRAP_WEBAPP_BASE_PATH = Environment.GetEnvironmentVariable(nameof(UNO_BOOTSTRAP_WEBAPP_BASE_PATH)) ?? "";
        private readonly BrowserHtmlElement _element;

        public CodeEditorPresenter()
        {
            Debug.WriteLine("CodeEditorPresenter()");
            Content = _element = BrowserHtmlElement.CreateHtmlElement("monaco-" + this.GetHashCode(), "div");

            LayoutUpdated += (s, e) =>
            {
                if (ParentCodeEditor is not null && ParentCodeEditor.IsEditorLoaded)
                {
                    NativeMethods.RefreshLayout(_element.ElementId);
                }
            };
        }

        public string ElementId => _element.ElementId;

        /// <inheritdoc />
        public event TypedEventHandler<ICodeEditorPresenter?, WebViewNewWindowRequestedEventArgs?>? NewWindowRequested; // ignored for now (external navigation)

        /// <inheritdoc />
        public event TypedEventHandler<ICodeEditorPresenter?, WebViewNavigationStartingEventArgs?>? NavigationStarting;

        /// <inheritdoc />
        public event TypedEventHandler<ICodeEditorPresenter?, WebViewNavigationCompletedEventArgs?>? NavigationCompleted; // ignored for now (only focus the editor)

        public CodeEditor? ParentCodeEditor { get; set; }

        public bool IsSettingValue
        {
            get => ParentCodeEditor?.IsSettingValue ?? false;
            set
            {
                ParentCodeEditor?.IsSettingValue = value;
            }
        }

        public bool TriggerKeyDown(WebKeyEventArgs args)
            => ParentCodeEditor?.TriggerKeyDown(args) ?? false;

        /// <inheritdoc />
        public global::System.Uri Source
        {
            get => new(NativeMethods.GetSrc(_element.ElementId));
            set
            {
                //var path = Environment.GetEnvironmentVariable("UNO_BOOTSTRAP_APP_BASE");
                //var target = $"/{path}/MonacoCodeEditor.html";
                //var target = (value.IsAbsoluteUri && value.IsFile)
                //	? value.PathAndQuery 
                //	: value.ToString();

                string target;
                if (value.IsAbsoluteUri)
                {
                    if (value.Scheme == "file")
                    {
                        // Local files are assumed as coming from the remoter server
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

                //NavigationStarting?.Invoke(this, new WebViewNavigationStartingEventArgs());
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => NavigationStarting?.Invoke(this, null));
            }
        }

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

                NativeMethods.RefreshLayout(_element.ElementId);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        static partial class NativeMethods
        {
            [JSImport("globalThis.getSrc")]
            public static partial string GetSrc(string elementId);

            [JSImport("globalThis.setSrc")]
            public static partial void SetSrc(string elementId, string src);

            [JSImport("globalThis.createMonacoEditor")]
            public static partial Task InitializeMonaco([JSMarshalAs<JSType.Any>] object managedOwner, string elementId, string baseUri);

            [JSImport("globalThis.refreshLayout")]
            public static partial void RefreshLayout(string elementId);
        }
    }
}