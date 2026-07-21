using Microsoft.UI.Xaml;

namespace Monaco.Helpers
{
    /// <summary>
    /// Interface for theme listener implementations.
    /// WASM uses the concrete ThemeListener with JSExport.
    /// Desktop will use a JsonRpc-based variant (Task 5).
    /// </summary>
    public interface IThemeListener
    {
        /// <summary>
        /// Gets the current theme name as a string.
        /// </summary>
        string CurrentThemeName { get; }

        /// <summary>
        /// Gets or sets the current application theme.
        /// </summary>
        ApplicationTheme CurrentTheme { get; set; }

        /// <summary>
        /// Gets or sets whether high contrast mode is active.
        /// </summary>
        bool IsHighContrast { get; set; }

        /// <summary>
        /// Raised when the application theme changes.
        /// </summary>
        event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
    }
}
