using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using SkiaSharp;

namespace WebView2FlickerSpike;

/// <summary>
/// Mode A: HWND-hosted WebView2 that replicates Uno's current approach.
/// WebView2 runs in the parent HWND via CoreWebView2Controller.
/// Skia renders an overlay rectangle to demonstrate the airspace problem:
/// the rectangle is drawn but hidden behind the WebView2 child HWND.
/// </summary>
internal sealed class HwndWebViewHost : IAsyncDisposable
{
    private readonly IntPtr _parentHwnd;
    private readonly string _contentPath;
    private CoreWebView2Controller? _controller;
    private CoreWebView2? _webView;
    private bool _isVisible = true;
    private float _opacity = 1.0f;
    private SKBitmap? _skiaBitmap;
    private int _lastWidth;
    private int _lastHeight;

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

    /// <summary>
    /// Renders Skia overlay content to the window DC via software rendering.
    /// In Mode A, the overlay rectangle is drawn but hidden behind the WebView2
    /// child HWND, demonstrating the airspace problem.
    /// </summary>
    public void RenderSkiaOverlay(IntPtr hwnd, int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        // Recreate bitmap if size changed
        if (_skiaBitmap is null || _lastWidth != width || _lastHeight != height)
        {
            _skiaBitmap?.Dispose();
            _skiaBitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            _lastWidth = width;
            _lastHeight = height;
        }

        using var canvas = new SKCanvas(_skiaBitmap);

        // Reuse the same rendering as Mode C to make the comparison fair.
        // In Mode A, this content is painted to the window DC but WebView2's
        // child HWND paints over it (airspace problem).
        DCompWebViewHost.RenderSkiaContent(canvas, width, height);
        canvas.Flush();

        // Blit the Skia bitmap to the window DC via GDI
        var hdc = GetDC(hwnd);
        if (hdc != IntPtr.Zero)
        {
            try
            {
                BlitSkiaBitmapToHdc(_skiaBitmap, hdc, width, height);
            }
            finally
            {
                ReleaseDC(hwnd, hdc);
            }
        }
    }

    private static void BlitSkiaBitmapToHdc(SKBitmap bitmap, IntPtr hdc, int width, int height)
    {
        var bmi = new BITMAPINFO
        {
            biSize = 40, // sizeof(BITMAPINFOHEADER)
            biWidth = width,
            biHeight = -height, // top-down DIB
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0, // BI_RGB
        };

        var pixels = bitmap.GetPixels();
        SetDIBitsToDevice(
            hdc,
            0, 0,
            (uint)width, (uint)height,
            0, 0,
            0, (uint)height,
            pixels,
            ref bmi,
            0 /* DIB_RGB_COLORS */);
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

        _skiaBitmap?.Dispose();
        _skiaBitmap = null;

        await ValueTask.CompletedTask;
    }

    // GDI P/Invoke for blitting Skia bitmap to window DC
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern int SetDIBitsToDevice(
        IntPtr hdc,
        int xDest, int yDest,
        uint dwWidth, uint dwHeight,
        int xSrc, int ySrc,
        uint uStartScan, uint cScanLines,
        IntPtr lpvBits,
        ref BITMAPINFO lpbmi,
        uint fuColorUse);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }
}
