# fn-15-spike-composition-mode-webview2-to.1 PoC: standalone Win32 + SkiaSharp + WebView2 spike

## Description

Build a standalone Win32 + SkiaSharp project with two WebView2 hosting modes:
- **Mode A**: HWND-hosted via `CoreWebView2Controller` — reproduces Uno's current flickering and airspace issues
- **Mode C**: Full DComp + ANGLE via `CoreWebView2CompositionController` — Skia and WebView2 as siblings in the same DComp visual tree

Both modes in the same window, toggled via keyboard, loading the same HTML content.

**Size:** M
**Files:** `spike/WebView2FlickerSpike/WebView2FlickerSpike.csproj`, `spike/WebView2FlickerSpike/Program.cs`, `spike/WebView2FlickerSpike/MainWindow.cs`, `spike/WebView2FlickerSpike/HwndWebViewHost.cs`, `spike/WebView2FlickerSpike/DCompWebViewHost.cs`, `spike/WebView2FlickerSpike/DCompInterop.cs`, `spike/WebView2FlickerSpike/AngleEglBridge.cs`, `spike/WebView2FlickerSpike/content/index.html`

## Approach

**Project setup:**
- `net10.0-windows10.0.22621` TFM (Windows 11 SDK)
- NuGet: `Microsoft.Web.WebView2`, `SkiaSharp`, `CsWin32`
- ANGLE native binaries: `libEGL.dll`, `libGLESv2.dll` (source from ANGLE project or extract from Chromium/Electron)
- Single executable, no Uno dependency
- Builds on macOS (cross-compile), runs on Windows 11

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

*Step 2: DComp setup*
- `DCompositionCreateDevice2(d3dDevice, IID_IDCompositionDevice, out device)`
- `device.CreateTargetForHwnd(hwnd, topmost: false, out target)`
- Create visuals: `rootVisual`, `skiaVisual`, `webViewVisual`
- `rootVisual.AddVisual(skiaVisual, ...)` then `rootVisual.AddVisual(webViewVisual, ...)`
- Z-order: Skia visual on top of WebView visual (proves airspace fix)

*Step 3: Skia rendering into DComp surface*
- `device.CreateVirtualSurface(width, height, DXGI_FORMAT_B8G8R8A8_UNORM, DXGI_ALPHA_MODE_PREMULTIPLIED, out surface)`
- Per-frame: `surface.BeginDraw(rect, IID_ID3D11Texture2D, out texture, out offset)`
- Wrap D3D11 texture as EGL surface: `eglCreatePbufferFromClientBuffer(display, EGL_D3D_TEXTURE_ANGLE, texture, ...)`
- Create Skia `GRBackendRenderTarget` from EGL surface framebuffer
- Render with SkiaSharp (background + overlapping rectangle)
- `surface.EndDraw()` → `device.Commit()`
- `skiaVisual.SetContent(surface)`
- Reference: Avalonia `DirectCompositedWindowSurface.cs`

*Step 4: WebView2 in DComp*
- `CreateCoreWebView2CompositionControllerAsync(hwnd)` → composition controller
- `compositionController.RootVisualTarget = webViewVisual`
- `controller.DefaultBackgroundColor = Color(0, 0, 0, 0)` (transparent)
- Window created with `WS_EX_NOREDIRECTIONBITMAP` (required — no GDI fallback)

*Step 5: Input forwarding*
- Intercept `WM_MOUSEMOVE`, `WM_LBUTTONDOWN`, `WM_LBUTTONUP`, `WM_MOUSEWHEEL` in WndProc
- Hit-test: is cursor in WebView2 bounds? Forward to `compositionController.SendMouseInput()`
- Otherwise: handled by Skia UI (if any)
- Handle `compositionController.CursorChanged` → `SetCursor()`

**Shared test content (`content/index.html`):**
- Dark background (`#1e1e1e`), heading, click counter button (JS)
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
- ANGLE binaries: can extract from Chrome/Electron installation, or build from source, or use a NuGet package like `ppy.osu.Framework.NativeLibs`. The specific DLLs needed are `libEGL.dll` and `libGLESv2.dll`.
- `IDCompositionVirtualSurface.BeginDraw` returns a `ID3D11Texture2D` — this is the key bridge point where ANGLE wraps the D3D texture as an EGL/GL surface for Skia.
- Avalonia's ANGLE integration files (`src/Windows/Avalonia.Win32/OpenGl/Angle/`) are the closest reference for the EGL↔D3D11 bridge pattern.
- The COM interop for DComp (`IDCompositionDevice`, `IDCompositionTarget`, `IDCompositionVisual`, `IDCompositionVirtualSurface`) can be generated by `CsWin32` or defined manually.
- `ICoreWebView2CompositionControllerInterop.put_RootVisualTarget` is a COM interop call — check if the managed WebView2 SDK exposes this directly or if raw COM is needed.

## Acceptance
- [ ] Standalone project builds on macOS (`dotnet build`), runs on Windows 11
- [ ] Mode A: Skia renders to window DC via WGL, WebView2 in child HWND
- [ ] Mode A: Skia-rendered rectangle is hidden behind WebView2 (airspace problem demonstrated)
- [ ] Mode C: Window uses `WS_EX_NOREDIRECTIONBITMAP`, all content via DComp
- [ ] Mode C: Skia renders into `IDCompositionVirtualSurface` via ANGLE
- [ ] Mode C: WebView2 renders into sibling DComp visual via `CoreWebView2CompositionController`
- [ ] Mode C: Skia-rendered rectangle visibly renders OVER WebView2 (airspace solved)
- [ ] Mode C: `DefaultBackgroundColor` set to transparent — no white flash
- [ ] Toggle between modes via keyboard
- [ ] Mouse input works in Mode C (clicks, hover in WebView content)
- [ ] JS interop works in both modes (`ExecuteScriptAsync`, `WebMessageReceived`)

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
