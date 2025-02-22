using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Monaco.Extensions;
using Uno.Foundation;
using Uno.Logging;
using Uno.Extensions;
using System.Runtime.InteropServices.JavaScript;
using Uno.UI.Runtime.Skia;

namespace Monaco
{
    public partial class CodeEditorPresenter : ContentControl, ICodeEditorPresenter
	{
		private static readonly string UNO_BOOTSTRAP_APP_BASE = global::System.Environment.GetEnvironmentVariable(nameof(UNO_BOOTSTRAP_APP_BASE));
		private static readonly string UNO_BOOTSTRAP_WEBAPP_BASE_PATH = Environment.GetEnvironmentVariable(nameof(UNO_BOOTSTRAP_WEBAPP_BASE_PATH)) ?? "";
        private readonly BrowserHtmlElement _element;

        public CodeEditorPresenter()
        {
            Console.WriteLine("CodeEditorPresenter()");
            Content = _element = BrowserHtmlElement.CreateHtmlElement("monaco-" + this.GetHashCode(), "div");
        }

        /// <inheritdoc />
        public event TypedEventHandler<ICodeEditorPresenter, WebViewNewWindowRequestedEventArgs> NewWindowRequested; // ignored for now (external navigation)

		/// <inheritdoc />
		public event TypedEventHandler<ICodeEditorPresenter, WebViewNavigationStartingEventArgs> NavigationStarting;

		/// <inheritdoc />
		public event TypedEventHandler<ICodeEditorPresenter, WebViewNavigationCompletedEventArgs> NavigationCompleted; // ignored for now (only focus the editor)

		/// <inheritdoc />
		public global::System.Uri Source
		{
			get => new global::System.Uri(NativeMethods.GetSrc(_element.ElementId));
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
                // await NativeMethods.InitializeMonaco(this, _element.ElementId, $"{UNO_BOOTSTRAP_WEBAPP_BASE_PATH}{UNO_BOOTSTRAP_APP_BASE}");
                await NativeMethods.InitializeMonaco(this, _element.ElementId, $"");
            }
            catch (Exception e)
			{
                Console.WriteLine(e);
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
        }
    }
}