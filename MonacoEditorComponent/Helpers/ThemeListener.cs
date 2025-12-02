using CommunityToolkit.WinUI;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

using System.Diagnostics;

using Windows.Foundation.Metadata;
using Windows.UI.ViewManagement;

namespace Monaco.Helpers
{
    public delegate void ThemeChangedEvent(ThemeListener sender);

    /// <summary>
    /// Class which listens for changes to Application Theme or High Contrast Modes 
    /// and Signals an Event when they occur.
    /// </summary>
    [AllowForWeb]
    public sealed partial class ThemeListener // This is a copy of the Toolkit ThemeListener, for some reason if we try and use it directly it's not read by the WebView
    {
        private readonly DispatcherQueue _queue;
        private readonly ICodeEditorPresenter _owner;

        public string CurrentThemeName { get { return CurrentTheme.ToString(); } } // For Web Retrieval

        public ApplicationTheme CurrentTheme { get; set; }
        public bool IsHighContrast { get; set; }

        public event ThemeChangedEvent? ThemeChanged;

        private readonly AccessibilitySettings _accessible = new();
        private readonly UISettings _settings = new();

        public ThemeListener(ICodeEditorPresenter presenter) : this(presenter, null) { }

        public ThemeListener(ICodeEditorPresenter presenter, DispatcherQueue? queue)
        {
            _queue = queue ?? DispatcherQueue.GetForCurrentThread();
            _owner = presenter;

            CurrentTheme = Application.Current.RequestedTheme;
#if !__WASM__
            IsHighContrast = _accessible.HighContrast;
#endif

            _accessible.HighContrastChanged += Accessible_HighContrastChanged;
            _settings.ColorValuesChanged += Settings_ColorValuesChanged;

            // Fallback in case either of the above fail, we'll check when we get activated next.
            if (Window.Current?.CoreWindow is not null)
            {
                Window.Current.CoreWindow.Activated += CoreWindow_Activated;
            }

            PartialCtor();
        }

        partial void PartialCtor();

        ~ThemeListener()
        {
            _accessible.HighContrastChanged -= Accessible_HighContrastChanged;
            _settings.ColorValuesChanged -= Settings_ColorValuesChanged;

            if (Window.Current?.CoreWindow is not null)
            {
                Window.Current.CoreWindow.Activated -= CoreWindow_Activated;
            }
        }

        private void Accessible_HighContrastChanged(AccessibilitySettings sender, object args)
        {
#if DEBUG
            Debug.WriteLine("HighContrast Changed");
#endif

            UpdateProperties();
        }

        // Note: This can get called multiple times during HighContrast switch, do we care?
        private async void Settings_ColorValuesChanged(UISettings sender, object args)
        {
            // Getting called off thread, so we need to dispatch to request value.
            await _queue.EnqueueAsync(() =>
            {
                // TODO: This doesn't stop the multiple calls if we're in our faked 'White' HighContrast Mode below.
                if (CurrentTheme != Application.Current.RequestedTheme ||
                    IsHighContrast != _accessible.HighContrast)
                {
#if DEBUG
                    Debug.WriteLine("Color Values Changed");
#endif

                    UpdateProperties();
                }
            });
        }

        private bool IsSystemHighContrast() =>
            ApiInformation.IsPropertyPresent("Windows.UI.ViewManagement.HighContrast", "HighContrast")
            && _accessible.HighContrast;

        private void CoreWindow_Activated(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.WindowActivatedEventArgs args)
        {
            if (CurrentTheme != Application.Current.RequestedTheme ||
                IsHighContrast != IsSystemHighContrast())
            {
#if DEBUG
                Debug.WriteLine("CoreWindow Activated Changed");
#endif

                UpdateProperties();
            }
        }

        /// <summary>
        /// Set our current properties and fire a change notification.
        /// </summary>
        private void UpdateProperties()
        {
            // TODO: Not sure if HighContrastScheme names are localized?
            if (IsSystemHighContrast() && _accessible.HighContrastScheme.Contains("white", StringComparison.OrdinalIgnoreCase))
            {
                // If our HighContrastScheme is ON & a lighter one, then we should remain in 'Light' theme mode for Monaco Themes Perspective
                IsHighContrast = false;
                CurrentTheme = ApplicationTheme.Light;
            }
            else
            {
                // Otherwise, we just set to what's in the system as we'd expect.
                IsHighContrast = _accessible.HighContrast;
                CurrentTheme = Application.Current.RequestedTheme;
            }

            ThemeChanged?.Invoke(this);
        }
    }
}
