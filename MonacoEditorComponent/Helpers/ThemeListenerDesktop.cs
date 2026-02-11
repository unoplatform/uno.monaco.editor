using System.Diagnostics;

using CommunityToolkit.WinUI;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

using Monaco.Bridge;

using StreamJsonRpc;

using Windows.UI.ViewManagement;

namespace Monaco.Helpers;

/// <summary>
/// Desktop implementation of <see cref="IThemeListener"/> that detects OS theme
/// changes and is also a JSON-RPC target for theme property queries from JS.
/// </summary>
internal sealed class ThemeListenerDesktop : IThemeListener, IDisposable
{
    private readonly DispatcherQueue _queue;
    private readonly AccessibilitySettings _accessible = new();
    private readonly UISettings _settings = new();
    private bool _disposed;

    public string CurrentThemeName => CurrentTheme.ToString();
    public ApplicationTheme CurrentTheme { get; set; }
    public bool IsHighContrast { get; set; }

    public event ThemeChangedEvent? ThemeChanged;

    public ThemeListenerDesktop(DispatcherQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        _queue = queue;
        CurrentTheme = Application.Current.RequestedTheme;
        IsHighContrast = _accessible.HighContrast;

        _accessible.HighContrastChanged += Accessible_HighContrastChanged;
        _settings.ColorValuesChanged += Settings_ColorValuesChanged;
    }

    // ============================================================
    // JSON-RPC target method
    // ============================================================

    [JsonRpcMethod("theme/getProperty")]
    public string OnGetThemeProperty(GetThemePropertyParams p)
    {
        return p.Name switch
        {
            "currentThemeName" => CurrentThemeName,
            "isHighContrast" => IsHighContrast.ToString(),
            _ => string.Empty,
        };
    }

    // ============================================================
    // OS theme change detection
    // ============================================================

    private void Accessible_HighContrastChanged(AccessibilitySettings sender, object args)
    {
        Debug.WriteLine("ThemeListenerDesktop: HighContrast Changed");
        UpdateProperties();
    }

    private async void Settings_ColorValuesChanged(UISettings sender, object args)
    {
        await _queue.EnqueueAsync(() =>
        {
            if (CurrentTheme != Application.Current.RequestedTheme ||
                IsHighContrast != _accessible.HighContrast)
            {
                Debug.WriteLine("ThemeListenerDesktop: Color Values Changed");
                UpdateProperties();
            }
        });
    }

    private void UpdateProperties()
    {
        if (_accessible.HighContrast &&
            _accessible.HighContrastScheme.Contains("white", StringComparison.OrdinalIgnoreCase))
        {
            IsHighContrast = false;
            CurrentTheme = ApplicationTheme.Light;
        }
        else
        {
            IsHighContrast = _accessible.HighContrast;
            CurrentTheme = Application.Current.RequestedTheme;
        }

        ThemeChanged?.Invoke(this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _accessible.HighContrastChanged -= Accessible_HighContrastChanged;
        _settings.ColorValuesChanged -= Settings_ColorValuesChanged;
    }
}
