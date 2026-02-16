using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace WebView2FlickerSpike;

/// <summary>
/// Main window that hosts both Mode A (HWND) and Mode C (DComp + ANGLE) WebView2 instances.
/// Toggle between modes using keyboard shortcuts.
///
/// Keyboard shortcuts:
///   [A] Switch to Mode A (HWND)
///   [C] Switch to Mode C (DComp + ANGLE)
///   [H] Show/Hide toggle
///   [R] Destroy and recreate WebView
///   [T] Create two WebViews (Mode A only for now)
///   [O] Animate opacity 0 -> 1
///   [M] Send message to WebView
///   [S] Execute script in WebView
/// </summary>
internal sealed class MainWindow : IAsyncDisposable
{
    private const string WindowClassName = "WebView2FlickerSpike";
    private const string ContentRelativePath = "content/index.html";

    // Window extended styles
    private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
    private const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
    private const int WS_VISIBLE = 0x10000000;

    // Window messages
    private const int WM_DESTROY = 0x0002;
    private const int WM_SIZE = 0x0005;
    private const int WM_PAINT = 0x000F;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_TIMER = 0x0113;

    // Virtual key codes
    private const int VK_A = 0x41;
    private const int VK_C = 0x43;
    private const int VK_H = 0x48;
    private const int VK_M = 0x4D;
    private const int VK_O = 0x4F;
    private const int VK_R = 0x52;
    private const int VK_S = 0x53;
    private const int VK_T = 0x54;

    // Timer IDs
    private const uint TIMER_OPACITY_ANIMATE = 1;
    private const uint TIMER_RENDER = 2;

    private IntPtr _hwnd;
    private HwndWebViewHost? _modeAHost;
    private DCompWebViewHost? _modeCHost;
    private ActiveMode _currentMode = ActiveMode.None;
    private float _animatingOpacity;
    private bool _isAnimating;
    private int _clientWidth;
    private int _clientHeight;
    private readonly string _contentPath;

    // Win32 delegates and P/Invoke
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate? _wndProc;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Win32RECT lpRect);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern bool GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursorW(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport("user32.dll")]
    private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);

    [DllImport("user32.dll")]
    private static extern bool KillTimer(IntPtr hWnd, IntPtr uIDEvent);

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32RECT
    {
        public int Left, Top, Right, Bottom;
    }

    public enum ActiveMode
    {
        None,
        ModeA_HWND,
        ModeC_DComp,
    }

    public MainWindow()
    {
        _contentPath = Path.Combine(AppContext.BaseDirectory, ContentRelativePath);
    }

    public void Create()
    {
        var hInstance = GetModuleHandleW(null);

        // Store the WndProc delegate to prevent GC collection
        _wndProc = WndProc;
        var fnPtr = Marshal.GetFunctionPointerForDelegate(_wndProc);

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = 0x0003, // CS_HREDRAW | CS_VREDRAW
            lpfnWndProc = fnPtr,
            hInstance = hInstance,
            hCursor = LoadCursorW(IntPtr.Zero, (IntPtr)32512), // IDC_ARROW
            hbrBackground = IntPtr.Zero,
            lpszClassName = WindowClassName,
        };

        RegisterClassExW(ref wc);

        // Start without WS_EX_NOREDIRECTIONBITMAP - will recreate window for Mode C
        _hwnd = CreateWindowExW(
            0,
            WindowClassName,
            "WebView2 Flicker Spike - Press [A] for HWND mode, [C] for DComp mode",
            WS_OVERLAPPEDWINDOW | WS_VISIBLE,
            100, 100, 1200, 800,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
        }

        ShowWindow(_hwnd, 1); // SW_SHOWNORMAL
        UpdateWindow(_hwnd);

        GetClientRect(_hwnd, out var rect);
        _clientWidth = rect.Right - rect.Left;
        _clientHeight = rect.Bottom - rect.Top;

        Console.WriteLine($"[Main] Window created: {_clientWidth}x{_clientHeight}");
        Console.WriteLine("[Main] Press [A] for Mode A (HWND), [C] for Mode C (DComp + ANGLE)");
        Console.WriteLine("[Main] Press [H] to toggle visibility, [R] to recreate");
        Console.WriteLine("[Main] Press [O] to animate opacity, [M] to send message, [S] to execute script");
    }

    public void RunMessageLoop()
    {
        while (GetMessageW(out var msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_SIZE:
                HandleResize(lParam);
                return IntPtr.Zero;

            case WM_KEYDOWN:
                HandleKeyDown((int)wParam);
                return IntPtr.Zero;

            case WM_MOUSEMOVE:
            case WM_LBUTTONDOWN:
            case WM_LBUTTONUP:
            case WM_MOUSEWHEEL:
                HandleMouseInput(msg, wParam, lParam);
                return IntPtr.Zero;

            case WM_TIMER:
                HandleTimer((uint)(int)wParam);
                return IntPtr.Zero;

            case WM_DESTROY:
                KillTimer(hWnd, (IntPtr)TIMER_RENDER);
                KillTimer(hWnd, (IntPtr)TIMER_OPACITY_ANIMATE);
                PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void HandleResize(IntPtr lParam)
    {
        _clientWidth = (short)(lParam.ToInt64() & 0xFFFF);
        _clientHeight = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

        switch (_currentMode)
        {
            case ActiveMode.ModeA_HWND:
                _modeAHost?.UpdateBounds(0, 0, _clientWidth, _clientHeight);
                break;
            case ActiveMode.ModeC_DComp:
                _modeCHost?.UpdateBounds(_clientWidth, _clientHeight);
                break;
        }
    }

    private void HandleKeyDown(int vkCode)
    {
        switch (vkCode)
        {
            case VK_A:
                _ = SwitchToModeAAsync();
                break;
            case VK_C:
                _ = SwitchToModeCAsync();
                break;
            case VK_H:
                ToggleVisibility();
                break;
            case VK_R:
                _ = RecreateWebViewAsync();
                break;
            case VK_O:
                StartOpacityAnimation();
                break;
            case VK_M:
                _ = SendTestMessageAsync();
                break;
            case VK_S:
                _ = ExecuteTestScriptAsync();
                break;
        }
    }

    private void HandleMouseInput(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (_currentMode != ActiveMode.ModeC_DComp || _modeCHost is null) return;

        int x = (short)(lParam.ToInt64() & 0xFFFF);
        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

        var eventKind = msg switch
        {
            WM_MOUSEMOVE => CoreWebView2MouseEventKind.Move,
            WM_LBUTTONDOWN => CoreWebView2MouseEventKind.LeftButtonDown,
            WM_LBUTTONUP => CoreWebView2MouseEventKind.LeftButtonUp,
            WM_MOUSEWHEEL => CoreWebView2MouseEventKind.Wheel,
            _ => CoreWebView2MouseEventKind.Move,
        };

        var virtualKeys = CoreWebView2MouseEventVirtualKeys.None;
        if (((int)wParam & 0x0001) != 0) virtualKeys |= CoreWebView2MouseEventVirtualKeys.LeftButton;
        if (((int)wParam & 0x0002) != 0) virtualKeys |= CoreWebView2MouseEventVirtualKeys.RightButton;

        uint mouseData = 0;
        if (msg == WM_MOUSEWHEEL)
        {
            mouseData = (uint)((int)wParam >> 16);
        }

        _modeCHost.SendMouseInput(eventKind, virtualKeys, mouseData, new System.Drawing.Point(x, y));
    }

    private void HandleTimer(uint timerId)
    {
        switch (timerId)
        {
            case TIMER_OPACITY_ANIMATE:
                AnimateOpacityStep();
                break;
            case TIMER_RENDER:
                _modeCHost?.RenderSkiaFrame();
                break;
        }
    }

    private async Task SwitchToModeAAsync()
    {
        if (_currentMode == ActiveMode.ModeA_HWND) return;

        Console.WriteLine("[Main] Switching to Mode A (HWND)...");

        // Tear down Mode C if active
        if (_modeCHost is not null)
        {
            KillTimer(_hwnd, (IntPtr)TIMER_RENDER);
            await _modeCHost.DisposeAsync();
            _modeCHost = null;
        }

        _currentMode = ActiveMode.ModeA_HWND;

        _modeAHost = new HwndWebViewHost(_hwnd, _contentPath);
        await _modeAHost.InitializeAsync();
        _modeAHost.UpdateBounds(0, 0, _clientWidth, _clientHeight);

        Console.WriteLine("[Main] Mode A active. WebView2 in child HWND.");
        Console.WriteLine("[Main] Note: Skia overlay rectangle is HIDDEN behind WebView2 (airspace problem).");
    }

    private async Task SwitchToModeCAsync()
    {
        if (_currentMode == ActiveMode.ModeC_DComp) return;

        Console.WriteLine("[Main] Switching to Mode C (DComp + ANGLE)...");

        // Tear down Mode A if active
        if (_modeAHost is not null)
        {
            await _modeAHost.DisposeAsync();
            _modeAHost = null;
        }

        _currentMode = ActiveMode.ModeC_DComp;

        _modeCHost = new DCompWebViewHost(_hwnd, _contentPath);
        await _modeCHost.InitializeAsync();
        _modeCHost.UpdateBounds(_clientWidth, _clientHeight);

        // Start render timer for DComp mode (16ms ~ 60fps)
        SetTimer(_hwnd, (IntPtr)TIMER_RENDER, 16, IntPtr.Zero);

        // Do initial render
        _modeCHost.RenderSkiaFrame();

        Console.WriteLine("[Main] Mode C active. Skia + WebView2 in DComp visual tree.");
        Console.WriteLine("[Main] Skia overlay rectangle should be VISIBLE over WebView2 (airspace solved).");
    }

    private void ToggleVisibility()
    {
        switch (_currentMode)
        {
            case ActiveMode.ModeA_HWND:
                _modeAHost?.ToggleVisibility();
                break;
            case ActiveMode.ModeC_DComp:
                _modeCHost?.ToggleVisibility();
                break;
        }
    }

    private async Task RecreateWebViewAsync()
    {
        Console.WriteLine("[Main] Recreating WebView...");
        switch (_currentMode)
        {
            case ActiveMode.ModeA_HWND:
                if (_modeAHost is not null)
                {
                    await _modeAHost.DisposeAsync();
                    _modeAHost = new HwndWebViewHost(_hwnd, _contentPath);
                    await _modeAHost.InitializeAsync();
                    _modeAHost.UpdateBounds(0, 0, _clientWidth, _clientHeight);
                }
                break;
            case ActiveMode.ModeC_DComp:
                if (_modeCHost is not null)
                {
                    KillTimer(_hwnd, (IntPtr)TIMER_RENDER);
                    await _modeCHost.DisposeAsync();
                    _modeCHost = new DCompWebViewHost(_hwnd, _contentPath);
                    await _modeCHost.InitializeAsync();
                    _modeCHost.UpdateBounds(_clientWidth, _clientHeight);
                    SetTimer(_hwnd, (IntPtr)TIMER_RENDER, 16, IntPtr.Zero);
                    _modeCHost.RenderSkiaFrame();
                }
                break;
        }
    }

    private void StartOpacityAnimation()
    {
        _animatingOpacity = 0f;
        _isAnimating = true;

        switch (_currentMode)
        {
            case ActiveMode.ModeA_HWND:
                _modeAHost?.SetOpacity(0f);
                break;
            case ActiveMode.ModeC_DComp:
                _modeCHost?.SetOpacity(0f);
                break;
        }

        SetTimer(_hwnd, (IntPtr)TIMER_OPACITY_ANIMATE, 16, IntPtr.Zero);
        Console.WriteLine("[Main] Starting opacity animation 0 -> 1...");
    }

    private void AnimateOpacityStep()
    {
        if (!_isAnimating) return;

        _animatingOpacity += 0.033f; // ~30 steps over 500ms at 60fps
        if (_animatingOpacity >= 1.0f)
        {
            _animatingOpacity = 1.0f;
            _isAnimating = false;
            KillTimer(_hwnd, (IntPtr)TIMER_OPACITY_ANIMATE);
            Console.WriteLine("[Main] Opacity animation complete.");
        }

        switch (_currentMode)
        {
            case ActiveMode.ModeA_HWND:
                _modeAHost?.SetOpacity(_animatingOpacity);
                break;
            case ActiveMode.ModeC_DComp:
                _modeCHost?.SetOpacity(_animatingOpacity);
                break;
        }
    }

    private async Task SendTestMessageAsync()
    {
        var message = $"Hello from host at {DateTime.Now:HH:mm:ss}";
        switch (_currentMode)
        {
            case ActiveMode.ModeA_HWND:
                if (_modeAHost is not null) await _modeAHost.SendMessageAsync(message);
                break;
            case ActiveMode.ModeC_DComp:
                if (_modeCHost is not null) await _modeCHost.SendMessageAsync(message);
                break;
        }
    }

    private async Task ExecuteTestScriptAsync()
    {
        const string script = "document.title + ' | clicks: ' + document.getElementById('clickCount').textContent";
        switch (_currentMode)
        {
            case ActiveMode.ModeA_HWND:
                if (_modeAHost is not null) await _modeAHost.ExecuteScriptAsync(script);
                break;
            case ActiveMode.ModeC_DComp:
                if (_modeCHost is not null) await _modeCHost.ExecuteScriptAsync(script);
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_modeAHost is not null)
        {
            await _modeAHost.DisposeAsync();
        }
        if (_modeCHost is not null)
        {
            await _modeCHost.DisposeAsync();
        }
    }
}
