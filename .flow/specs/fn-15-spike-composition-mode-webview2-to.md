# Spike: WebView2 Flicker Repro & Full DComp Fix

## Goal

Build a standalone Win32 project (no Uno) with two modes:
- **Mode A (HWND)**: Reproduces the flickering — Skia renders to window DC, WebView2 in a child HWND. This is what Uno does today.
- **Mode C (Full DComp + ANGLE)**: Fixes both flickering AND airspace — Skia renders into a DComp surface via ANGLE, WebView2 renders into a sibling DComp visual. Both in the same composition tree.

Same window, same content, toggle between modes, see the difference.

## Why Mode C (not just overlay)

The DComp overlay approach (Skia on Layer 1, WebView2 on Layer 2) fixes flickering but NOT airspace — WebView2 is still always on top of Skia content. Mode C puts both Skia and WebView2 into the same DComp visual tree as siblings, enabling true z-ordering. This is what WinUI does natively and what Avalonia implements.

## Architecture

### Mode A: HWND (current Uno behavior)

```
Win32 Window
├── Layer 1: Skia → WGL → SwapBuffers → window DC
└── Layer 3: Child HWND → CoreWebView2Controller
    (always on top, HWND airspace problem, flash on teardown)
```

### Mode C: Full DComp + ANGLE

```
Win32 Window (WS_EX_NOREDIRECTIONBITMAP)
└── DComp Target
    ├── Visual A: Skia → OpenGL ES (ANGLE) → D3D11 texture → IDCompositionSurface
    └── Visual B: WebView2 → CoreWebView2CompositionController → RootVisualTarget
    (true compositing, z-orderable, no airspace, no flash)
```

## Mode A — HWND-hosted (replicates Uno)

Replicates `Win32NativeWebView.cs` + `GlRenderer` from `unoplatform/uno`:
- Parent window with WGL + SkiaSharp rendering (Layer 1)
- Child HWND via `CreateWindowEx`, `SetParent`
- `CoreWebView2Environment.CreateAsync()` → `CreateCoreWebView2ControllerAsync(childHwnd)`
- Visibility via `ShowWindow` / `SetWindowPos`
- `WS_EX_LAYERED` for opacity

## Mode C — Full DComp + ANGLE

**Window:**
- `CreateWindowEx` with `WS_EX_NOREDIRECTIONBITMAP` — tells DWM not to create a redirection surface
- No GDI/WGL rendering to window DC — all content goes through DComp

**DComp setup:**
- `DCompositionCreateDevice2(d3dDevice, IID_IDCompositionDevice, out device)`
- `device.CreateTargetForHwnd(hwnd, topmost: false, out target)`
- Create root visual, Skia visual, WebView2 visual
- `target.SetRoot(rootVisual)` → `rootVisual.AddVisual(skiaVisual)` → `rootVisual.AddVisual(webViewVisual)`
- Z-order controlled by visual ordering in the tree

**Skia rendering into DComp (via ANGLE):**
- Create D3D11 device
- Create `IDCompositionVirtualSurface` for Skia content
- `surface.BeginDraw(rect, IID_ID3D11Texture2D, out texture)` → get D3D11 texture
- Initialize ANGLE EGL display backed by the D3D11 device
- Wrap the D3D11 texture as an EGL surface via `eglCreatePbufferFromClientBuffer`
- Create Skia `GRContext` from the ANGLE GL context
- Create `GRBackendRenderTarget` from the EGL surface
- Render with SkiaSharp
- `surface.EndDraw()` → `device.Commit()`
- Reference: Avalonia's `DirectCompositedWindowSurface.cs` (173 lines)

**WebView2 in DComp:**
- `CoreWebView2Environment.CreateAsync()` → `CreateCoreWebView2CompositionControllerAsync(hwnd)`
- `compositionController.RootVisualTarget = webViewVisual`
- `controller.DefaultBackgroundColor = transparent`
- Input forwarding via `SendMouseInput()` / `SendPointerInput()`
- Handle `CursorChanged`

**Z-ordering demo:**
- Render a Skia rectangle that partially overlaps WebView2
- In Mode A: rectangle hidden behind WebView2 (airspace)
- In Mode C: rectangle renders on top (proper compositing)

## Test Scenarios (run in both modes)

| # | Scenario | What to observe |
|---|----------|----------------|
| 1 | **Show/Hide toggle** | Rapidly toggle visibility 10x. Count white flashes. |
| 2 | **Dark theme load** | Dark background. Any white flash on initial load? |
| 3 | **Resize** | Drag window edge. Flicker during resize? |
| 4 | **Destroy/recreate** | Destroy WebView, recreate (simulates tab switch). Flash? |
| 5 | **Two WebViews** | Two WebViews, toggle visibility independently. Z-order correct? |
| 6 | **Skia over WebView** | Skia-rendered element overlapping WebView2. Visible in both modes? |

## Dependencies

- `net10.0-windows10.0.22621` (Windows 11 SDK)
- `Microsoft.Web.WebView2` NuGet
- `SkiaSharp` NuGet
- `CsWin32` NuGet (P/Invoke source gen)
- ANGLE native binaries (`libEGL.dll`, `libGLESv2.dll`) — can reference via `Microsoft.AspNetCore.Components.WebView` or package directly from [Google's ANGLE builds](https://chromium.googlesource.com/angle/angle)

## References

- [Avalonia DirectCompositedWindowSurface.cs](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Windows/Avalonia.Win32/DComposition/DirectCompositedWindowSurface.cs) — BeginDraw/EndDraw pattern
- [Avalonia AngleWin32EglDisplay.cs](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Windows/Avalonia.Win32/OpenGl/Angle/) — ANGLE D3D11 integration
- [WebView2 API Sample ViewComponent.cpp](https://github.com/MicrosoftEdge/WebView2Samples) — DComp visual tree setup
- [Wice Framework WebView.cs](https://github.com/aelyo-softworks/Wice) — .NET DComp + WebView2
- [CoreWebView2CompositionController API](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2compositioncontroller)
- [Windowed vs Visual hosting](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/windowed-vs-visual-hosting)

## Quick commands

```bash
# Build (works on macOS — cross-compiles for Windows)
dotnet build spike/WebView2FlickerSpike/WebView2FlickerSpike.csproj

# Run (Windows only)
dotnet run --project spike/WebView2FlickerSpike/WebView2FlickerSpike.csproj
```

## Acceptance

- [ ] Standalone project builds on macOS, runs on Windows 11
- [ ] Mode A: HWND-hosted WebView2 reproduces visible flickering in at least 2 scenarios
- [ ] Mode C: DComp + ANGLE hosts both Skia and WebView2 in same composition tree
- [ ] Mode C: Skia-rendered element visibly renders OVER WebView2 (airspace solved)
- [ ] Mode C: no white flash on show/hide, dark theme load, destroy/recreate
- [ ] Mouse input works in Mode C
- [ ] JS interop works in both modes
- [ ] Results documented: scenario × mode comparison table
