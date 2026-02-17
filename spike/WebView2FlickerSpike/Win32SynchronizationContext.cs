using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace WebView2FlickerSpike;

/// <summary>
/// A custom <see cref="SynchronizationContext"/> that marshals callbacks to the Win32 UI thread
/// using PostMessage/SendMessage with a custom WM_APP+N message.
///
/// This is necessary because the spike uses a raw Win32 GetMessage/DispatchMessage loop
/// (no WinForms Application.Run), so no built-in SynchronizationContext is installed.
/// WindowsFormsSynchronizationContext would silently fail because it depends on Control.BeginInvoke
/// and WinForms message infrastructure.
///
/// Usage:
///   1. Create an instance with the main window HWND
///   2. Install via SynchronizationContext.SetSynchronizationContext before the message loop
///   3. In WndProc, handle <see cref="WM_DISPATCH_CALLBACK"/> by calling <see cref="DrainCallbacks"/>
/// </summary>
internal sealed class Win32SynchronizationContext : SynchronizationContext
{
    /// <summary>
    /// Custom window message ID used to signal that callbacks are queued for dispatch.
    /// WM_APP (0x8000) through 0xBFFF are reserved for application use.
    /// </summary>
    public const int WM_DISPATCH_CALLBACK = 0x8000; // WM_APP + 0

    private readonly IntPtr _hwnd;
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly int _uiThreadId;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentThreadId();

    /// <summary>
    /// Creates a new Win32SynchronizationContext targeting the specified window handle.
    /// Must be created on the UI thread.
    /// </summary>
    /// <param name="hwnd">The main window handle to post messages to.</param>
    public Win32SynchronizationContext(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _uiThreadId = GetCurrentThreadId();
    }

    // Private constructor for CreateCopy - shares the queue and targets same HWND
    private Win32SynchronizationContext(IntPtr hwnd, ConcurrentQueue<(SendOrPostCallback, object?)> queue, int uiThreadId)
    {
        _hwnd = hwnd;
        _queue = queue;
        _uiThreadId = uiThreadId;
    }

    /// <inheritdoc />
    public override void Post(SendOrPostCallback d, object? state)
    {
        _queue.Enqueue((d, state));
        PostMessageW(_hwnd, (uint)WM_DISPATCH_CALLBACK, IntPtr.Zero, IntPtr.Zero);
    }

    /// <inheritdoc />
    public override void Send(SendOrPostCallback d, object? state)
    {
        if (GetCurrentThreadId() == _uiThreadId)
        {
            // Already on the UI thread - invoke directly
            d(state);
        }
        else
        {
            // Marshal to UI thread via SendMessage (blocks until processed)
            _queue.Enqueue((d, state));
            SendMessageW(_hwnd, (uint)WM_DISPATCH_CALLBACK, IntPtr.Zero, IntPtr.Zero);
        }
    }

    /// <inheritdoc />
    public override SynchronizationContext CreateCopy()
    {
        return new Win32SynchronizationContext(_hwnd, _queue, _uiThreadId);
    }

    /// <summary>
    /// Drains all queued callbacks and invokes them on the current (UI) thread.
    /// Must be called from the WndProc handler for <see cref="WM_DISPATCH_CALLBACK"/>.
    /// </summary>
    public void DrainCallbacks()
    {
        while (_queue.TryDequeue(out var item))
        {
            try
            {
                item.Callback(item.State);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SyncCtx] Unhandled exception in callback: {ex}");
            }
        }
    }
}
