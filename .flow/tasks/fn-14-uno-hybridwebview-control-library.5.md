# fn-14-uno-hybridwebview-control-library.5 WASM iframe presenter

## Description
Implement the WASM presenter that hosts local content in an iframe and bridges C#↔JS communication via postMessage and JSImport/JSExport.

**Size:** M
**Files:** `HybridWebViewComponent/HybridWebView/WasmHybridWebViewPresenter.cs`, `HybridWebViewComponent/HybridWebView/WasmHybridWebViewPresenter.wasm.cs` (JS interop methods)

## Approach

- Implement `IHybridWebViewPresenter` for WASM (BrowserHtmlElement + iframe)
- Follow patterns from `MonacoEditorComponent/CodeEditor/WasmCodeEditorPresenter.cs`:
  - `BrowserHtmlElement` for DOM manipulation
  - `JSImport`/`JSExport` for direct WASM↔JS interop
  - Element visibility management

- **Content hosting**:
  - Create an `<iframe>` element via BrowserHtmlElement
  - Set `src` to the app-relative path: `{AppBaseUri}/{HybridRoot}/{DefaultFile}`
  - User content must be included as static assets in the WASM app (via MSBuild `Content` items)
  - Inject `HybridWebView.js` bridge into the iframe content (either via `<script>` in user's HTML, or auto-injected)

- **Message routing**:
  - Parent↔iframe communication via `window.postMessage` / `window.addEventListener("message", ...)`
  - C#→JS: post structured message to iframe's `contentWindow.postMessage()`
  - JS→C#: iframe posts message to `window.parent.postMessage()`, caught by JSExport handler
  - `EvaluateJavaScriptAsync`: post an eval request message, await response
  - `InvokeJavaScriptAsync<T>`: post invoke message with task ID, await response via TaskManager
  - Response routing via `HybridWebViewTaskManager`

- **WASM-specific considerations**:
  - iframe must be same-origin to allow postMessage without CORS issues — content is served from same origin
  - No `WebResourceRequested` equivalent — content must be real files served by the WASM host
  - Consider using `srcdoc` attribute for simple content or blob URLs as fallback

- **Lifecycle**:
  - `InitializeAsync`: create iframe element, set src, wait for load event
  - `DisposeAsync`: remove iframe, cancel in-flight tasks
  - Handle iframe load errors

## Key context

- On WASM, the app IS the browser — there's no WebView2 process. The iframe provides isolation.
- `WasmCodeEditorPresenter.cs` uses `BrowserHtmlElement.CreateHtmlElement("iframe")` pattern
- MAUI has NO WASM target — this presenter is entirely new engineering, not a port
- `JSImport`/`JSExport` requires `[JSExport]` on static partial methods with `System.Runtime.InteropServices.JavaScript`
- Content files in WASM apps are served from the app base URL (e.g., `/wwwroot/index.html`)
## Acceptance
- [ ] `WasmHybridWebViewPresenter` implements `IHybridWebViewPresenter`
- [ ] iframe created via `BrowserHtmlElement` to host user content
- [ ] Content loaded from app-relative path (`{HybridRoot}/{DefaultFile}`)
- [ ] `HybridWebView.js` bridge functional within iframe
- [ ] C#→JS: `EvaluateJavaScriptAsync` and `InvokeJavaScriptAsync` work via postMessage to iframe
- [ ] JS→C#: iframe postMessage dispatches to registered .NET methods via JSExport
- [ ] Raw messages: `SendRawMessage` delivers via postMessage
- [ ] Lifecycle: proper iframe creation, load detection, teardown
- [ ] Same-origin policy satisfied (iframe content same origin as app)
- [ ] Project builds successfully
## Done summary
- Task completed
## Evidence
- Commits:
- Tests:
- PRs: