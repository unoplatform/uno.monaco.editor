using WebView2FlickerSpike;

/// <summary>
/// Entry point for the WebView2 Flicker Spike application.
///
/// This standalone Win32 application demonstrates two WebView2 hosting modes:
///
/// Mode A (HWND): Replicates Uno's current approach where WebView2 runs in a child HWND.
///   - Demonstrates airspace problem (Skia content hidden behind WebView2)
///   - Demonstrates flickering on show/hide/resize
///
/// Mode C (DComp + ANGLE): Full DirectComposition integration where Skia and WebView2
///   are siblings in the same DComp visual tree.
///   - Eliminates airspace problem (Skia overlays visible over WebView2)
///   - Eliminates flickering (no HWND show/hide transitions)
///   - Transparent WebView2 background (no white flash)
///
/// Cross-compiles on macOS, runs only on Windows 11.
///
/// Dependencies:
///   - Microsoft.Web.WebView2 (CoreWebView2CompositionController)
///   - SkiaSharp (Skia rendering)
///   - CsWin32 (Win32 P/Invoke source gen)
///   - ANGLE native binaries (libEGL.dll, libGLESv2.dll) - must be in the output directory
///
/// Usage:
///   [A] Switch to Mode A (HWND)
///   [C] Switch to Mode C (DComp + ANGLE)
///   [H] Show/Hide toggle
///   [R] Destroy and recreate WebView
///   [O] Animate opacity 0 -> 1
///   [M] Send message to WebView
///   [S] Execute script in WebView
/// </summary>

Console.WriteLine("=== WebView2 Flicker Spike ===");
Console.WriteLine("Two modes in one window: HWND (Mode A) vs DComp+ANGLE (Mode C)");
Console.WriteLine();

try
{
    var window = new MainWindow();
    window.Create();
    window.RunMessageLoop();
    await window.DisposeAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex}");
    Environment.ExitCode = 1;
}
