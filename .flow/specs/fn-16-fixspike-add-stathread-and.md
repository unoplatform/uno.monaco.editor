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

## ANGLE Binary Sourcing (Pre-flight Required)

Before implementation, validate ANGLE binaries work for D3D11 interop:

**Primary source**: Extract `libEGL.dll` + `libGLESv2.dll` from the `Microsoft.AspNetCore.Components.WebView` NuGet package. These are the ANGLE binaries Microsoft ships for Blazor Hybrid — they support D3D11 interop and are already tested on Windows.

**Fallback**: Extract from a local Chrome installation (`C:\Program Files\Google\Chrome\Application\{version}\`).

**Pre-flight validation (Task 1a must do this first)**:
1. Extract ANGLE DLLs from chosen source
2. Call `eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, ...)` with `EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE`
3. Verify the display initializes — if it fails, try the fallback source before proceeding

## DComp COM Interop Strategy

The DComp API surface from C# requires:
- `DCompositionCreateDevice2` (exported function)
- `IDCompositionDevice` → `CreateTargetForHwnd`, `CreateVisual`, `CreateVirtualSurface`, `Commit`
- `IDCompositionTarget` → `SetRoot`
- `IDCompositionVisual` → `SetContent`, `AddVisual`, `SetOffsetX/Y`
- `IDCompositionVirtualSurface` → `BeginDraw` (returns `ID3D11Texture2D`), `EndDraw`, `Resize`

**Primary approach**: CsWin32 source-generated bindings via `Microsoft.Windows.SDK.Win32Metadata`.

**Validation step (Task 1a)**: Create a minimal console app that calls `DCompositionCreateDevice2`, creates a visual tree, and commits it — before integrating Skia or WebView2. If CsWin32 doesn't generate working bindings (e.g., `IDCompositionVirtualSurface.BeginDraw` output parameter typing issues), fall back to manual COM interface definitions (~150 lines of `[ComImport]` boilerplate).

**RootVisualTarget**: `CoreWebView2CompositionController.RootVisualTarget` expects an `object` that must be a COM-castable `IDCompositionVisual`. May require `Marshal.GetIUnknownForObject` or raw COM pointers if CsWin32-generated types aren't accepted. The Wice framework's source code demonstrates this pattern.

## Per-Frame Rendering Lifecycle (DComp + ANGLE + Skia)

The DComp texture from `BeginDraw` is **transient** — only valid between `BeginDraw` and `EndDraw`. Each frame follows this lifecycle:

1. `surface.BeginDraw(rect, IID_ID3D11Texture2D, out texture, out offset)` → get D3D11 texture
2. Wrap as EGL surface: `eglCreatePbufferFromClientBuffer(display, EGL_D3D_TEXTURE_ANGLE, texture, ...)`
3. Create `GRBackendRenderTarget` from the EGL surface framebuffer
4. Render with SkiaSharp
5. Dispose render target and EGL surface
6. `surface.EndDraw()` → `device.Commit()`

**Key constraints**:
- The D3D11 texture from `BeginDraw` must come from the **same D3D11 device** that backs the ANGLE EGL display. Pass your D3D11 device (via `IDXGIDevice` QI) to `DCompositionCreateDevice2`.
- The `GRContext` can be reused across frames (create once from ANGLE GL context). The render target is transient.
- Ensure ANGLE's EGL context is current (`eglMakeCurrent`) when creating the `GRContext`.

**Reference**: Avalonia's `DirectCompositedWindowSurface.cs:BeginDrawCore()` and `EndDrawCore()` methods specifically.

## WebView2 API Usage

**The spike does NOT use Uno's `WebView2` control.** It directly uses `CoreWebView2Environment` and `CoreWebView2CompositionController` from the `Microsoft.Web.WebView2.Core` NuGet package. All WebView2 interaction bypasses Uno's presenter layer.

This is architecturally important: the spike proves that the Windows composition approach works at the Win32/COM level. Integrating this back into Uno's `DesktopCodeEditorPresenter` is a separate, follow-up effort.

## Input Forwarding (Minimal for Spike)

For the spike, input forwarding is scoped to **basic mouse interaction** only:
- Intercept `WM_MOUSEMOVE`, `WM_LBUTTONDOWN`, `WM_LBUTTONUP`, `WM_MOUSEWHEEL` in WndProc
- Hit-test: is cursor in WebView2 bounds? Forward to `compositionController.SendMouseInput()`
- Handle `compositionController.CursorChanged` → `SetCursor()`

**Explicitly deferred** to the Uno integration task:
- Full pointer API (`WM_POINTERDOWN/UP/UPDATE`, `POINTER_INFO` structs, `SendPointerInput()`)
- DPI scaling (coordinates in WebView2 local space accounting for DComp visual offsets)
- Keyboard focus management (`MoveFocus()`, `WM_KEYDOWN/UP` forwarding)
- Touch input

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
- See "Per-Frame Rendering Lifecycle" section above for the full `BeginDraw → EGL wrap → Skia render → EGL destroy → EndDraw → Commit` cycle

**WebView2 in DComp:**
- `CoreWebView2Environment.CreateAsync()` → `CreateCoreWebView2CompositionControllerAsync(hwnd)`
- `compositionController.RootVisualTarget = webViewVisual` (may require `Marshal.GetIUnknownForObject` — see DComp COM Interop section)
- `controller.DefaultBackgroundColor = transparent`

## Test Scenarios (run in both modes)

| # | Scenario | What to observe |
|---|----------|----------------|
| 1 | **Show/Hide toggle** | Rapidly toggle visibility 10x. Count white flashes. |
| 2 | **Dark theme load** | Dark background. Any white flash on initial load? |
| 3 | **Resize** | Drag window edge. Flicker during resize? |
| 4 | **Destroy/recreate** | Destroy WebView, recreate (simulates tab switch). Flash? |
| 5 | **Two WebViews** | Two WebViews, toggle visibility independently. Z-order correct? |
| 6 | **Skia over WebView** | Skia-rendered element overlapping WebView2. Visible in both modes? |
| 7 | **Opacity animation** | Animate opacity 0→1 over 500ms. Smooth? |
| 8 | **WebView2 transparency** | `DefaultBackgroundColor = transparent`, load page with CSS `rgba` regions. Verify Skia content visible through transparent WebView2 regions in Mode C. |

## Dependencies

- `net10.0-windows10.0.22621` (Windows 11 SDK)
- `Microsoft.Web.WebView2` NuGet
- `SkiaSharp` NuGet
- `CsWin32` NuGet (P/Invoke source gen)
- ANGLE native binaries (`libEGL.dll`, `libGLESv2.dll`) — see "ANGLE Binary Sourcing" section above

## Abort Criteria

Abandon the spike if any of the following are true after 2 days of investigation:
1. CsWin32 cannot generate usable DComp bindings AND manual COM definitions exceed 300 lines
2. ANGLE cannot wrap DComp `BeginDraw` textures as EGL surfaces (D3D11 device mismatch or unsupported texture format)
3. `CoreWebView2CompositionController.RootVisualTarget` rejects CsWin32-generated or manually-defined DComp visuals
4. The ANGLE binaries from both primary and fallback sources fail to initialize with `EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE`

If abandoned, document which step failed and why, to inform alternative approaches (e.g., C++/WinRT spike instead of managed code).

## References

- [Avalonia DirectCompositedWindowSurface.cs](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Windows/Avalonia.Win32/DComposition/DirectCompositedWindowSurface.cs) — BeginDraw/EndDraw pattern (specifically `BeginDrawCore()` and `EndDrawCore()`)
- [Avalonia AngleWin32EglDisplay.cs](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Windows/Avalonia.Win32/OpenGl/Angle/) — ANGLE D3D11 integration
- [WebView2 API Sample ViewComponent.cpp](https://github.com/MicrosoftEdge/WebView2Samples) — DComp visual tree setup
- [Wice Framework WebView.cs](https://github.com/aelyo-softworks/Wice) — .NET DComp + WebView2 (raw COM pointer pattern for RootVisualTarget)
- [CoreWebView2CompositionController API](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2compositioncontroller)
- [Windowed vs Visual hosting](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/windowed-vs-visual-hosting)

## Quick commands

```bash
# Build (cross-compiles on macOS for Windows)
dotnet build spike/WebView2FlickerSpike/WebView2FlickerSpike.csproj

# Run (Windows only)
dotnet run --project spike/WebView2FlickerSpike/WebView2FlickerSpike.csproj
```

## Acceptance

- [ ] Project compiles on macOS via cross-compilation (`dotnet build`). Runs and validates only on Windows 11
- [ ] Mode A: HWND-hosted WebView2 — flickering tested in all 8 scenarios (see Test Scenarios section for Mode A expectations)
- [ ] Mode C: DComp + ANGLE hosts both Skia and WebView2 in same composition tree
- [ ] Mode C: Skia-rendered element visibly renders OVER WebView2 (airspace solved)
- [ ] Mode C: no white flash on show/hide, dark theme load, destroy/recreate
- [ ] Mode C: WebView2 transparency — Skia content visible through transparent CSS regions
- [ ] Mouse input works in Mode C (clicks, hover in WebView content)
- [ ] JS interop works in both modes (`ExecuteScriptAsync`, `WebMessageReceived`)
- [ ] Toggle between modes via keyboard
- [ ] Results documented: scenario × mode comparison table with clear observations
