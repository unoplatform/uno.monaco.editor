# fn-15-spike-composition-mode-webview2-to.1 PoC: standalone Win32 + SkiaSharp + WebView2 spike

## Description

Build a standalone Win32 + SkiaSharp project with two WebView2 hosting modes:
- **Mode A**: HWND-hosted via `CoreWebView2Controller` — reproduces Uno's current flickering and airspace issues
- **Mode C**: Full DComp + ANGLE via `CoreWebView2CompositionController` — Skia and WebView2 as siblings in the same DComp visual tree

Both modes in the same window, toggled via keyboard, loading the same HTML content.

**The spike does NOT use Uno's `WebView2` control.** It directly uses `CoreWebView2Environment` and `CoreWebView2CompositionController` from the `Microsoft.Web.WebView2.Core` NuGet package, bypassing Uno's presenter layer entirely.

**Size:** L (DComp + ANGLE + Skia + WebView2 COM interop chain is substantial)
**Files:** `spike/WebView2FlickerSpike/WebView2FlickerSpike.csproj`, `spike/WebView2FlickerSpike/Program.cs`, `spike/WebView2FlickerSpike/MainWindow.cs`, `spike/WebView2FlickerSpike/HwndWebViewHost.cs`, `spike/WebView2FlickerSpike/DCompWebViewHost.cs`, `spike/WebView2FlickerSpike/DCompInterop.cs`, `spike/WebView2FlickerSpike/AngleEglBridge.cs`, `spike/WebView2FlickerSpike/content/index.html`

## Approach

### Phase 1a: Pre-flight Validation (do this FIRST)

Before building the full spike, validate the three critical integration points:

**1. ANGLE binary sourcing + D3D11 init:**
- Extract `libEGL.dll` + `libGLESv2.dll` from the `Microsoft.AspNetCore.Components.WebView` NuGet package (primary source — Microsoft ships these for Blazor Hybrid, known D3D11 interop support)
- Fallback: extract from local Chrome installation (`C:\Program Files\Google\Chrome\Application\{version}\`)
- Create a minimal console app that calls `eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, ...)` with `EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE`
- Verify the EGL display initializes successfully
- If both sources fail, trigger abort criteria #4

**2. CsWin32 DComp bindings validation:**
- Create a minimal console app that:
  - Creates a D3D11 device
  - Calls `DCompositionCreateDevice2(dxgiDevice, IID_IDCompositionDevice, out device)` (note: requires `IDXGIDevice` via QI, not `ID3D11Device` directly)
  - Creates a target for an HWND, creates a visual tree, commits
- If CsWin32 doesn't generate usable bindings (known risk: `IDCompositionVirtualSurface.BeginDraw` output parameter typing), fall back to manual `[ComImport]` COM interface definitions (~150 lines)
- If manual definitions exceed 300 lines, trigger abort criteria #1

**3. RootVisualTarget COM interop:**
- Test that `CoreWebView2CompositionController.RootVisualTarget` accepts the DComp visual (CsWin32-generated or manual)
- May require `Marshal.GetIUnknownForObject` or raw COM pointers (reference: Wice framework pattern)
- If rejected, trigger abort criteria #3

### Phase 1b: Full Implementation

**Project setup:**
- `net10.0-windows10.0.22621` TFM (Windows 11 SDK)
- NuGet: `Microsoft.Web.WebView2`, `SkiaSharp`, `CsWin32`
- ANGLE native binaries from pre-flight validated source
- Single executable, no Uno dependency
- Cross-compiles on macOS (`dotnet build`), runs only on Windows 11

**Mode A — `HwndWebViewHost` (replicate Uno):**
- Parent window: register class, `CreateWindowEx(WS_OVERLAPPEDWINDOW)`, WGL + SkiaSharp to window DC
- Child HWND: `CreateWindowEx` → `SetParent(childHwnd, parentHwnd)`
- `CoreWebView2Environment.CreateAsync()` → `CreateCoreWebView2ControllerAsync(childHwnd)`
- `controller.Bounds` for sizing, `ShowWindow`/`SetWindowPos` for visibility
- `WS_EX_LAYERED` + `SetLayeredWindowAttributes` for opacity
- Skia renders background + overlapping rectangle to parent window DC (demonstrates airspace — rectangle hidden behind WebView2)

**Mode C — `DCompWebViewHost` (full DComp + ANGLE):**

*Step 1: D3D11 + ANGLE setup*
- Create D3D11 device via `D3D11CreateDevice`
- Initialize ANGLE EGL display backed by the D3D11 device (`eglGetPlatformDisplayEXT` with `EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE`)
- Initialize EGL, choose config, create EGL context
- Create `GRContext` from the ANGLE GL context (ensure ANGLE EGL context is current via `eglMakeCurrent`). The `GRContext` can be reused across frames.

*Step 2: DComp setup*
- `DCompositionCreateDevice2(dxgiDevice, IID_IDCompositionDevice, out device)` — pass the **same D3D11 device** (via `IDXGIDevice` QI) that backs the ANGLE EGL display
- `device.CreateTargetForHwnd(hwnd, topmost: false, out target)`
- Create visuals: `rootVisual`, `skiaVisual`, `webViewVisual`
- `rootVisual.AddVisual(skiaVisual, ...)` then `rootVisual.AddVisual(webViewVisual, ...)`
- Z-order: Skia visual on top of WebView visual (proves airspace fix)

*Step 3: Skia rendering into DComp surface (per-frame lifecycle)*
- `device.CreateVirtualSurface(width, height, DXGI_FORMAT_B8G8R8A8_UNORM, DXGI_ALPHA_MODE_PREMULTIPLIED, out surface)`
- Per-frame cycle (texture is transient — cannot cache across frames):
  1. `surface.BeginDraw(rect, IID_ID3D11Texture2D, out texture, out offset)` → get D3D11 texture
  2. Wrap as EGL surface: `eglCreatePbufferFromClientBuffer(display, EGL_D3D_TEXTURE_ANGLE, texture, ...)`
  3. Create `GRBackendRenderTarget` from EGL surface framebuffer
  4. Render with SkiaSharp (background + overlapping rectangle)
  5. Dispose render target and EGL surface
  6. `surface.EndDraw()` → `device.Commit()`
- `skiaVisual.SetContent(surface)`
- Reference: Avalonia `DirectCompositedWindowSurface.cs:BeginDrawCore()` and `EndDrawCore()` methods

*Step 4: WebView2 in DComp*
- `CreateCoreWebView2CompositionControllerAsync(hwnd)` → composition controller
- `compositionController.RootVisualTarget = webViewVisual` (using validated COM interop approach from pre-flight)
- `controller.DefaultBackgroundColor = Color(0, 0, 0, 0)` (transparent)
- Window created with `WS_EX_NOREDIRECTIONBITMAP` (required — no GDI fallback)

*Step 5: Input forwarding (minimal for spike)*
- Intercept `WM_MOUSEMOVE`, `WM_LBUTTONDOWN`, `WM_LBUTTONUP`, `WM_MOUSEWHEEL` in WndProc
- Hit-test: is cursor in WebView2 bounds? Forward to `compositionController.SendMouseInput()`
- Handle `compositionController.CursorChanged` → `SetCursor()`
- **Explicitly deferred to Uno integration**: full pointer API (`SendPointerInput`, `POINTER_INFO`), DPI scaling, keyboard focus management (`MoveFocus()`), touch input

**Shared test content (`content/index.html`):**
- Dark background (`#1e1e1e`), heading, click counter button (JS)
- Partial transparency section (CSS `rgba`) for transparency scenario testing
- `chrome.webview.postMessage` for bidirectional message testing
- High contrast against white flash

**Keyboard shortcuts:**
- [A] Switch to Mode A (HWND)
- [C] Switch to Mode C (DComp + ANGLE)
- [H] Show/Hide toggle
- [R] Destroy and recreate WebView
- [T] Create two WebViews
- [O] Animate opacity 0→1

## Key context

- Window must use `WS_EX_NOREDIRECTIONBITMAP` in Mode C — this tells DWM not to create a redirection surface. Without it, DComp content won't display (black window). In Mode A, do NOT use this flag.
- ANGLE binaries: primary source is `Microsoft.AspNetCore.Components.WebView` NuGet (Microsoft's ANGLE for Blazor Hybrid). Fallback: Chrome installation. Both must support `EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE`.
- `IDCompositionVirtualSurface.BeginDraw` returns a `ID3D11Texture2D` — this is the key bridge point. The texture is **transient** (valid only between BeginDraw/EndDraw).
- The D3D11 device passed to `DCompositionCreateDevice2` must be the same device backing ANGLE's EGL display. Use `IDXGIDevice` QI.
- Avalonia's ANGLE integration files (`src/Windows/Avalonia.Win32/OpenGl/Angle/`) and `DirectCompositedWindowSurface.cs` are the primary references.
- `CoreWebView2CompositionController.RootVisualTarget` is a COM interop call — may need `Marshal.GetIUnknownForObject` (see Wice framework reference).

## Acceptance
- [ ] Pre-flight: ANGLE EGL display initializes with D3D11 backend from validated binary source
- [ ] Pre-flight: CsWin32 DComp bindings work (or manual COM defs < 300 lines)
- [ ] Pre-flight: `RootVisualTarget` accepts DComp visual via validated interop pattern
- [ ] Project compiles on macOS via cross-compilation (`dotnet build`). Runs and validates only on Windows 11
- [ ] Mode A: Skia renders to window DC via WGL, WebView2 in child HWND
- [ ] Mode A: Skia-rendered rectangle is hidden behind WebView2 (airspace problem demonstrated)
- [ ] Mode C: Window uses `WS_EX_NOREDIRECTIONBITMAP`, all content via DComp
- [ ] Mode C: Skia renders into `IDCompositionVirtualSurface` via ANGLE (per-frame BeginDraw/EndDraw lifecycle)
- [ ] Mode C: WebView2 renders into sibling DComp visual via `CoreWebView2CompositionController`
- [ ] Mode C: Skia-rendered rectangle visibly renders OVER WebView2 (airspace solved)
- [ ] Mode C: `DefaultBackgroundColor` set to transparent — no white flash
- [ ] Mode C: Skia content visible through transparent CSS `rgba` regions in WebView2
- [ ] Toggle between modes via keyboard
- [ ] Mouse input works in Mode C (clicks, hover in WebView content — basic `SendMouseInput` only)
- [ ] JS interop works in both modes (`ExecuteScriptAsync`, `WebMessageReceived`)

## Done summary
Standalone Win32 spike project with two WebView2 hosting modes: Mode A (HWND-hosted, demonstrates airspace problem) and Mode C (full DComp+ANGLE composition with Skia and WebView2 as sibling visuals, eliminates airspace). Includes manual COM interop for DirectComposition, ANGLE EGL bridge for D3D11 texture wrapping, per-frame Skia rendering lifecycle, mouse input forwarding, and JS interop. Cross-compiles on macOS, runs on Windows 11.
## Evidence
- Commits: c9495576abbd8a56660d8f6e7ec365540f7edfd9, 897c31128e7b9e6e5e19d71b5cd0e24c9d7daadc
- Tests: dotnet build spike/WebView2FlickerSpike/WebView2FlickerSpike.csproj, dotnet build MonacoEditorComponent.slnx --no-restore
- PRs: