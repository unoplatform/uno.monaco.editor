using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace WebView2FlickerSpike;

/// <summary>
/// Mode A: HWND-hosted WebView2 that replicates Uno's current approach.
/// Skia renders to the parent window's DC, WebView2 runs in a child HWND.
/// This demonstrates the airspace problem - Skia content is always hidden behind WebView2.
/// </summary>
internal sealed class HwndWebViewHost : IAsyncDisposable
{
    private readonly IntPtr _parentHwnd;
    private readonly string _contentPath;
    private CoreWebView2Controller? _controller;
    private CoreWebView2? _webView;
    private bool _isVisible = true;
    private float _opacity = 1.0f;

    public bool IsInitialized => _controller is not null;
    public bool IsVisible => _isVisible;
    public float Opacity => _opacity;

    public event Action<string>? WebMessageReceived;

    public HwndWebViewHost(IntPtr parentHwnd, string contentPath)
    {
        _parentHwnd = parentHwnd;
        _contentPath = contentPath;
    }

    public async Task InitializeAsync()
    {
        if (_controller is not null) return;

        Console.WriteLine("[Mode A] Initializing HWND WebView2...");

        var env = await CoreWebView2Environment.CreateAsync();
        _controller = await env.CreateCoreWebView2ControllerAsync(_parentHwnd);
        _webView = _controller.CoreWebView2;

        // Dark background to match content
        _controller.DefaultBackgroundColor = Color.FromArgb(255, 30, 30, 30);

        _webView.WebMessageReceived += (_, args) =>
        {
            var message = args.WebMessageAsJson;
            Console.WriteLine($"[Mode A] WebMessage received: {message}");
            WebMessageReceived?.Invoke(message);
        };

        _webView.NavigationCompleted += (_, args) =>
        {
            Console.WriteLine($"[Mode A] Navigation completed. Success: {args.IsSuccess}");
        };

        // Navigate to the local HTML content
        var uri = new Uri(Path.GetFullPath(_contentPath));
        _webView.Navigate(uri.AbsoluteUri);

        Console.WriteLine("[Mode A] WebView2 initialized and navigating.");
    }

    public void UpdateBounds(int x, int y, int width, int height)
    {
        if (_controller is null) return;
        _controller.Bounds = new Rectangle(x, y, width, height);
    }

    public void SetVisible(bool visible)
    {
        if (_controller is null) return;
        _isVisible = visible;
        _controller.IsVisible = visible;
        Console.WriteLine($"[Mode A] Visibility set to: {visible}");
    }

    public void ToggleVisibility()
    {
        SetVisible(!_isVisible);
    }

    public void SetOpacity(float opacity)
    {
        _opacity = Math.Clamp(opacity, 0f, 1f);
        // HWND mode: opacity requires WS_EX_LAYERED + SetLayeredWindowAttributes
        // For the spike, we just log it - true opacity on child HWNDs is limited
        Console.WriteLine($"[Mode A] Opacity set to: {_opacity} (limited in HWND mode)");
    }

    public async Task ExecuteScriptAsync(string script)
    {
        if (_webView is null) return;
        var result = await _webView.ExecuteScriptAsync(script);
        Console.WriteLine($"[Mode A] Script result: {result}");
    }

    public async Task SendMessageAsync(string message)
    {
        if (_webView is null) return;
        _webView.PostWebMessageAsString(message);
        Console.WriteLine($"[Mode A] Message sent: {message}");
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_controller is not null)
        {
            Console.WriteLine("[Mode A] Disposing HWND WebView2...");
            _controller.Close();
            _controller = null;
            _webView = null;
        }
        await ValueTask.CompletedTask;
    }
}
