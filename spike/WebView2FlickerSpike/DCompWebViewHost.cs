using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using SkiaSharp;

namespace WebView2FlickerSpike;

/// <summary>
/// Mode C: Full DComp + ANGLE WebView2 host.
/// Both Skia and WebView2 render into sibling DirectComposition visuals,
/// enabling true z-ordering and eliminating airspace problems.
///
/// Architecture:
///   Win32 Window (WS_EX_NOREDIRECTIONBITMAP)
///   +-- DComp Target
///       +-- rootVisual
///           +-- webViewVisual (bottom) -- WebView2 via CompositionController
///           +-- skiaVisual (top)       -- Skia via ANGLE + DComp surface
///
/// Per-frame rendering lifecycle:
///   1. surface.BeginDraw -> get transient D3D11 texture
///   2. Wrap as EGL surface via ANGLE
///   3. Render with SkiaSharp
///   4. Destroy EGL surface (transient)
///   5. surface.EndDraw -> device.Commit
/// </summary>
internal sealed class DCompWebViewHost : IAsyncDisposable
{
    private readonly IntPtr _hwnd;
    private readonly string _contentPath;

    // D3D11
    private IntPtr _d3dDevice;
    private IntPtr _dxgiDevice;

    // ANGLE EGL
    private IntPtr _eglDisplay;
    private IntPtr _eglConfig;
    private IntPtr _eglContext;
    private IntPtr _eglStableSurface; // Kept alive for context validity between frames

    // SkiaSharp
    private GRContext? _grContext;

    // DComp - COM RCWs released explicitly in DisposeAsync in reverse creation order
    private IDCompositionDevice? _dcompDevice;
    private IDCompositionTarget? _dcompTarget;
    private IDCompositionVisual? _rootVisual;
    private IDCompositionVisual? _skiaVisual;
    private IDCompositionVisual? _webViewVisual;
    private IDCompositionVirtualSurface? _dcompSurface;

    // WebView2 - CompositionController extends Controller, so we use it for both
    private CoreWebView2CompositionController? _compositionController;
    private CoreWebView2? _webView;

    private int _width;
    private int _height;
    private bool _isVisible = true;
    private float _opacity = 1.0f;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;
    public bool IsVisible => _isVisible;
    public float Opacity => _opacity;

    public event Action<string>? WebMessageReceived;

    // D3D11 interop
    private static class D3D11
    {
        [DllImport("d3d11.dll", PreserveSig = false)]
        public static extern void D3D11CreateDevice(
            IntPtr pAdapter,
            int DriverType, // D3D_DRIVER_TYPE_HARDWARE = 1
            IntPtr Software,
            uint Flags, // D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20
            IntPtr pFeatureLevels,
            uint FeatureLevels,
            uint SDKVersion, // D3D11_SDK_VERSION = 7
            out IntPtr ppDevice,
            out int pFeatureLevel,
            out IntPtr ppImmediateContext);
    }

    public DCompWebViewHost(IntPtr hwnd, string contentPath)
    {
        _hwnd = hwnd;
        _contentPath = contentPath;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        Console.WriteLine("[Mode C] Initializing DComp + ANGLE pipeline...");

        // Step 1: Create D3D11 device
        InitializeD3D11();

        // Step 2: Initialize ANGLE EGL backed by D3D11
        InitializeAngle();

        // Step 3: Create SkiaSharp GRContext from ANGLE GL context
        InitializeSkia();

        // Step 4: Setup DComp visual tree
        InitializeDComp();

        // Step 5: Initialize WebView2 in composition mode
        await InitializeWebView2Async();

        _isInitialized = true;
        Console.WriteLine("[Mode C] Full initialization complete.");
    }

    private void InitializeD3D11()
    {
        Console.WriteLine("[Mode C] Creating D3D11 device...");

        // D3D_DRIVER_TYPE_HARDWARE = 1
        // D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20 (required for DComp)
        // D3D11_SDK_VERSION = 7
        D3D11.D3D11CreateDevice(
            IntPtr.Zero,    // Default adapter
            1,              // Hardware driver
            IntPtr.Zero,    // No software rasterizer
            0x20,           // BGRA support
            IntPtr.Zero,    // Default feature levels
            0,
            7,              // SDK version
            out _d3dDevice,
            out int featureLevel,
            out IntPtr immediateContext);

        // Release the immediate context - we don't need it directly
        if (immediateContext != IntPtr.Zero)
        {
            Marshal.Release(immediateContext);
        }

        Console.WriteLine($"[Mode C] D3D11 device created. Feature level: 0x{featureLevel:X}");

        // QI for IDXGIDevice (required by DCompositionCreateDevice2)
        var iidDxgiDevice = DCompInterop.IID_IDXGIDevice;
        var hr = Marshal.QueryInterface(_d3dDevice, in iidDxgiDevice, out _dxgiDevice);
        if (hr != 0)
        {
            throw new InvalidOperationException($"QueryInterface for IDXGIDevice failed: 0x{hr:X}");
        }

        Console.WriteLine("[Mode C] IDXGIDevice obtained.");
    }

    private void InitializeAngle()
    {
        Console.WriteLine("[Mode C] Initializing ANGLE EGL with D3D11 backend...");

        (_eglDisplay, _eglConfig, _eglContext) = AngleEglBridge.InitializeAngle(_d3dDevice);

        // Create a stable 1x1 pbuffer surface to keep the EGL context valid between frames.
        // The context must be bound to a live surface during GRContext creation in InitializeSkia.
        // This surface is kept alive for the lifetime of the host and destroyed in DisposeAsync.
        int[] pbufferAttribs =
        [
            AngleEglBridge.EGL_WIDTH, 1,
            AngleEglBridge.EGL_HEIGHT, 1,
            AngleEglBridge.EGL_NONE
        ];
        _eglStableSurface = AngleEglBridge.eglCreatePbufferSurface(_eglDisplay, _eglConfig, pbufferAttribs);
        if (_eglStableSurface == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"eglCreatePbufferSurface failed: 0x{AngleEglBridge.eglGetError():X}");
        }

        if (!AngleEglBridge.eglMakeCurrent(_eglDisplay, _eglStableSurface, _eglStableSurface, _eglContext))
        {
            throw new InvalidOperationException(
                $"eglMakeCurrent failed: 0x{AngleEglBridge.eglGetError():X}");
        }

        Console.WriteLine("[Mode C] ANGLE EGL initialized and context made current.");
    }

    private void InitializeSkia()
    {
        Console.WriteLine("[Mode C] Creating SkiaSharp GRContext from ANGLE GL context...");

        // Use CreateAngle() which is specifically designed for ANGLE EGL contexts.
        // The EGL context is current with the stable pbuffer surface from InitializeAngle.
        var glInterface = GRGlInterface.CreateAngle();
        if (glInterface is null)
        {
            throw new InvalidOperationException("Failed to create GRGlInterface for ANGLE.");
        }

        _grContext = GRContext.CreateGl(glInterface);
        if (_grContext is null)
        {
            throw new InvalidOperationException("Failed to create GRContext from ANGLE GL context.");
        }

        Console.WriteLine("[Mode C] GRContext created successfully.");
    }

    private void InitializeDComp()
    {
        Console.WriteLine("[Mode C] Setting up DirectComposition visual tree...");

        // Create DComp device using the same DXGI device that backs ANGLE
        DCompInterop.DCompositionCreateDevice2(
            _dxgiDevice,
            DCompInterop.IID_IDCompositionDevice,
            out object dcompDeviceObj);

        _dcompDevice = (IDCompositionDevice)dcompDeviceObj;

        // Create target for the HWND
        _dcompDevice.CreateTargetForHwnd(_hwnd, false, out _dcompTarget);

        // Create visual tree: root -> [webViewVisual (bottom), skiaVisual (top)]
        _dcompDevice.CreateVisual(out _rootVisual);
        _dcompDevice.CreateVisual(out _skiaVisual);
        _dcompDevice.CreateVisual(out _webViewVisual);

        // Build tree: webView at bottom, skia on top (proves airspace fix)
        _rootVisual.AddVisual(_webViewVisual, false, null);  // bottom
        _rootVisual.AddVisual(_skiaVisual, true, _webViewVisual);  // above webview

        _dcompTarget.SetRoot(_rootVisual);
        _dcompDevice.Commit();

        Console.WriteLine("[Mode C] DComp visual tree created.");
    }

    private async Task InitializeWebView2Async()
    {
        Console.WriteLine("[Mode C] Creating WebView2 CompositionController...");

        var env = await CoreWebView2Environment.CreateAsync();

        // CompositionController extends Controller - all controller properties
        // (Bounds, IsVisible, CoreWebView2, DefaultBackgroundColor, Close) are inherited
        _compositionController = await env.CreateCoreWebView2CompositionControllerAsync(_hwnd);
        _webView = _compositionController.CoreWebView2;

        // Transparent background - critical for Mode C to avoid white flash
        _compositionController.DefaultBackgroundColor = Color.FromArgb(0, 0, 0, 0);

        // Set the RootVisualTarget to our DComp visual
        // This is the key integration: WebView2 renders into the DComp visual tree
        _compositionController.RootVisualTarget = _webViewVisual;

        // Wire up cursor changes
        _compositionController.CursorChanged += (_, _) =>
        {
            // Get the system cursor handle and apply it
            try
            {
                var cursorHandle = _compositionController.Cursor;
                Win32Helpers.SetCursor(cursorHandle);
            }
            catch
            {
                // Best effort cursor update
            }
        };

        _webView.WebMessageReceived += (_, args) =>
        {
            var message = args.WebMessageAsJson;
            Console.WriteLine($"[Mode C] WebMessage received: {message}");
            WebMessageReceived?.Invoke(message);
        };

        _webView.NavigationCompleted += (_, args) =>
        {
            Console.WriteLine($"[Mode C] Navigation completed. Success: {args.IsSuccess}");
        };

        // Navigate to the local HTML content
        var uri = new Uri(Path.GetFullPath(_contentPath));
        _webView.Navigate(uri.AbsoluteUri);

        Console.WriteLine("[Mode C] WebView2 CompositionController initialized.");
    }

    public void UpdateBounds(int width, int height)
    {
        _width = width;
        _height = height;

        if (_compositionController is not null)
        {
            _compositionController.Bounds = new Rectangle(0, 0, width, height);
        }

        // Resize the DComp surface; on failure it will be recreated in EnsureDCompSurface
        if (_dcompSurface is not null)
        {
            try
            {
                _dcompSurface.Resize((uint)width, (uint)height);
            }
            catch
            {
                // Release the failed surface COM object to avoid leaking, then
                // null it so EnsureDCompSurface recreates it on next render.
                try { Marshal.ReleaseComObject(_dcompSurface); } catch { /* best effort */ }
                _dcompSurface = null;
            }
        }
    }

    /// <summary>
    /// Renders a Skia frame into the DComp surface via ANGLE.
    /// Per-frame lifecycle: BeginDraw -> ANGLE wrap -> Skia render -> EndDraw -> Commit
    /// </summary>
    public void RenderSkiaFrame()
    {
        if (!_isInitialized || _dcompDevice is null || _grContext is null) return;
        if (_width <= 0 || _height <= 0) return;

        bool drawStarted = false;
        try
        {
            // Ensure we have a DComp virtual surface
            EnsureDCompSurface();

            if (_dcompSurface is null) return;

            // Step 1: BeginDraw - get transient D3D11 texture
            var updateRect = new RECT(0, 0, _width, _height);
            _dcompSurface.BeginDraw(
                ref updateRect,
                DCompInterop.IID_ID3D11Texture2D,
                out object textureObj,
                out POINT offset);
            drawStarted = true;

            var texturePtr = Marshal.GetIUnknownForObject(textureObj);
            try
            {
                // Step 2: Wrap D3D11 texture as EGL surface via ANGLE
                var eglSurface = AngleEglBridge.CreateSurfaceFromD3DTexture(
                    _eglDisplay, _eglConfig, texturePtr, _width, _height);

                try
                {
                    // Make ANGLE context current with the new surface
                    AngleEglBridge.eglMakeCurrent(_eglDisplay, eglSurface, eglSurface, _eglContext);

                    // Step 3: Get framebuffer info for Skia
                    AngleEglBridge.glGetIntegerv(AngleEglBridge.GL_FRAMEBUFFER_BINDING, out int fbId);

                    var glInfo = new GRGlFramebufferInfo((uint)fbId, 0x8058); // GL_RGBA8
                    using var renderTarget = new GRBackendRenderTarget(
                        _width, _height, 0, 8, glInfo);

                    using var surface = SKSurface.Create(
                        _grContext, renderTarget, GRSurfaceOrigin.TopLeft, SKColorType.Rgba8888);

                    if (surface is not null)
                    {
                        var canvas = surface.Canvas;
                        RenderSkiaContent(canvas, _width, _height);
                        canvas.Flush();
                        _grContext.Flush();
                    }

                    AngleEglBridge.glFinish();
                }
                finally
                {
                    // Step 4: Destroy transient EGL surface, restore stable surface.
                    // Rebind stable surface so GRContext has a valid context for any
                    // lazy cleanup (texture eviction etc.) between frames.
                    AngleEglBridge.eglMakeCurrent(_eglDisplay, _eglStableSurface, _eglStableSurface, _eglContext);
                    AngleEglBridge.eglDestroySurface(_eglDisplay, eglSurface);
                }
            }
            finally
            {
                // Step 5: EndDraw BEFORE releasing the texture RCW.
                // DComp's EndDraw commits the surface content and may reference the
                // D3D11 texture internally. Releasing the RCW first would destroy the
                // COM object (refcount → 0) causing a use-after-free.
                _dcompSurface.EndDraw();
                drawStarted = false;

                Marshal.Release(texturePtr);
                Marshal.ReleaseComObject(textureObj);
            }

            // Step 6: Commit the DComp frame
            _dcompDevice.Commit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Mode C] RenderSkiaFrame error: {ex.Message}");
            if (drawStarted)
            {
                try { _dcompSurface?.EndDraw(); } catch { /* best effort */ }
            }
        }
    }

    private void EnsureDCompSurface()
    {
        if (_dcompSurface is not null || _dcompDevice is null || _skiaVisual is null) return;

        _dcompDevice.CreateVirtualSurface(
            (uint)_width,
            (uint)_height,
            DCompInterop.DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            DCompInterop.DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_PREMULTIPLIED,
            out _dcompSurface);

        // Always set content when creating/recreating the surface so the visual
        // references the correct surface instance after resize failures.
        _skiaVisual.SetContent(_dcompSurface);

        Console.WriteLine($"[Mode C] DComp virtual surface created: {_width}x{_height}");
    }

    /// <summary>
    /// Renders demo Skia content: background + overlapping rectangle that proves
    /// z-ordering works (visible OVER WebView2 in Mode C).
    /// </summary>
    internal static void RenderSkiaContent(SKCanvas canvas, int width, int height)
    {
        // Clear with semi-transparent dark background
        canvas.Clear(new SKColor(30, 30, 30, 200));

        // Draw a title bar area
        using var titlePaint = new SKPaint
        {
            Color = new SKColor(40, 40, 40, 230),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        canvas.DrawRect(0, 0, width, 40, titlePaint);

        // Draw title text (SkiaSharp 3.x: use SKFont + DrawText with font parameter)
        using var textFont = new SKFont { Size = 16 };
        using var textPaint = new SKPaint
        {
            Color = new SKColor(200, 200, 200),
            IsAntialias = true,
        };
        canvas.DrawText("Mode C: Skia + DComp (this overlay is ABOVE WebView2)", 12, 26, SKTextAlign.Left, textFont, textPaint);

        // Draw the key demo element: a colored rectangle that overlaps the WebView2 area.
        // In Mode A (HWND), this same rectangle is rendered but hidden behind WebView2.
        // In Mode C (DComp), this is visible ON TOP of WebView2.
        using var rectPaint = new SKPaint
        {
            Color = new SKColor(220, 50, 50, 180), // Semi-transparent red
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        float rectX = width - 220;
        float rectY = 60;
        canvas.DrawRoundRect(rectX, rectY, 200, 120, 8, 8, rectPaint);

        // Label for the overlapping rectangle
        using var labelFont = new SKFont { Size = 14 };
        using var labelPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
        };
        canvas.DrawText("Skia overlay", rectX + 50, rectY + 45, SKTextAlign.Left, labelFont, labelPaint);
        canvas.DrawText("(over WebView2)", rectX + 38, rectY + 65, SKTextAlign.Left, labelFont, labelPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = new SKColor(80, 80, 80, 200),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
        };
        canvas.DrawRect(1, 1, width - 2, height - 2, borderPaint);

        // Draw a small status indicator
        using var statusFont = new SKFont { Size = 12 };
        using var statusPaint = new SKPaint
        {
            Color = new SKColor(78, 201, 176, 255), // Teal
            IsAntialias = true,
        };
        canvas.DrawText("Airspace: SOLVED - Skia renders over WebView2", 12, height - 12, SKTextAlign.Left, statusFont, statusPaint);
    }

    /// <summary>
    /// Forwards mouse input to the WebView2 CompositionController.
    /// </summary>
    public void SendMouseInput(CoreWebView2MouseEventKind eventKind, CoreWebView2MouseEventVirtualKeys virtualKeys, uint mouseData, System.Drawing.Point point)
    {
        if (_compositionController is null) return;

        try
        {
            _compositionController.SendMouseInput(eventKind, virtualKeys, mouseData, point);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Mode C] SendMouseInput error: {ex.Message}");
        }
    }

    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (_compositionController is not null)
        {
            _compositionController.IsVisible = visible;
        }
        Console.WriteLine($"[Mode C] Visibility set to: {visible}");
    }

    public void ToggleVisibility()
    {
        SetVisible(!_isVisible);
    }

    public void SetOpacity(float opacity)
    {
        _opacity = Math.Clamp(opacity, 0f, 1f);
        // In DComp mode, opacity can be set on the visual via SetOpacity (not exposed in our interface)
        // For the spike, we log it
        Console.WriteLine($"[Mode C] Opacity set to: {_opacity}");
    }

    public async Task ExecuteScriptAsync(string script)
    {
        if (_webView is null) return;
        var result = await _webView.ExecuteScriptAsync(script);
        Console.WriteLine($"[Mode C] Script result: {result}");
    }

    public async Task SendMessageAsync(string message)
    {
        if (_webView is null) return;
        _webView.PostWebMessageAsString(message);
        Console.WriteLine($"[Mode C] Message sent: {message}");
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("[Mode C] Disposing DComp WebView host...");

        // Close WebView2 first
        if (_compositionController is not null)
        {
            _compositionController.Close();
            _compositionController = null;
            _webView = null;
        }

        // Release DComp COM objects in reverse creation order.
        // Explicit release ensures deterministic cleanup (GC finalization order is undefined)
        // and prevents stale references in the composition tree.
        if (_dcompSurface is not null) { Marshal.ReleaseComObject(_dcompSurface); _dcompSurface = null; }
        if (_skiaVisual is not null) { Marshal.ReleaseComObject(_skiaVisual); _skiaVisual = null; }
        if (_webViewVisual is not null) { Marshal.ReleaseComObject(_webViewVisual); _webViewVisual = null; }
        if (_rootVisual is not null) { Marshal.ReleaseComObject(_rootVisual); _rootVisual = null; }
        if (_dcompTarget is not null) { Marshal.ReleaseComObject(_dcompTarget); _dcompTarget = null; }
        if (_dcompDevice is not null) { Marshal.ReleaseComObject(_dcompDevice); _dcompDevice = null; }

        // Dispose Skia GRContext before tearing down EGL
        _grContext?.Dispose();
        _grContext = null;

        // Tear down EGL: destroy stable surface, context, then terminate display
        if (_eglDisplay != IntPtr.Zero)
        {
            AngleEglBridge.eglMakeCurrent(_eglDisplay, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (_eglStableSurface != IntPtr.Zero)
            {
                AngleEglBridge.eglDestroySurface(_eglDisplay, _eglStableSurface);
                _eglStableSurface = IntPtr.Zero;
            }

            if (_eglContext != IntPtr.Zero)
            {
                AngleEglBridge.eglDestroyContext(_eglDisplay, _eglContext);
                _eglContext = IntPtr.Zero;
            }

            AngleEglBridge.eglTerminate(_eglDisplay);
            _eglDisplay = IntPtr.Zero;
        }

        if (_dxgiDevice != IntPtr.Zero)
        {
            Marshal.Release(_dxgiDevice);
            _dxgiDevice = IntPtr.Zero;
        }

        if (_d3dDevice != IntPtr.Zero)
        {
            Marshal.Release(_d3dDevice);
            _d3dDevice = IntPtr.Zero;
        }

        _isInitialized = false;
        Console.WriteLine("[Mode C] Disposed.");
        await ValueTask.CompletedTask;
    }
}

/// <summary>
/// P/Invoke helpers for cursor management.
/// Named to avoid collision with CsWin32-generated NativeMethods partial class.
/// </summary>
internal static class Win32Helpers
{
    [DllImport("user32.dll")]
    public static extern IntPtr SetCursor(IntPtr hCursor);
}
