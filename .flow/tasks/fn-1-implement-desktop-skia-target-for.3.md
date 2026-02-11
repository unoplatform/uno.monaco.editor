## Description

Create the HTML host page for desktop WebView2, define the JSON-RPC 2.0 bridge protocol contract, bundle Monaco Editor from its ESM distribution via esbuild (replacing the deprecated AMD loader), integrate `vscode-jsonrpc` as the JS-side JSON-RPC client, and configure dual resource packaging with explicit MSBuild targets.

**Size:** L (expanded: Monaco ESM migration + build pipeline overhaul + bridge + HTML + packaging)
**Files:** MonacoEditorComponent/DesktopContent/editor.html (new), MonacoEditorComponent/DesktopContent/bridge-protocol.md (new), MonacoEditorComponent/ts-helpermethods/bridge/jsonRpcBridge.ts (new), MonacoEditorComponent/ts-helpermethods/index.ts (new), MonacoEditorComponent/ts-helpermethods/esbuild.config.mjs (new), MonacoEditorComponent/tsconfig.json (update), MonacoEditorComponent/MonacoEditorComponent.csproj, package.json (add monaco-editor + vscode-jsonrpc + esbuild), install-dependencies.ps1 (update)

## Approach

### Monaco ESM migration (replacing deprecated AMD)

Monaco Editor deprecated AMD in v0.53.0 and recommends ESM. The current vendored `monaco-editor/min/vs/` AMD distribution (loaded via `loader.js` + `require()`) is replaced with the ESM distribution bundled by esbuild.

**Key change**: Instead of loading Monaco at runtime via AMD `require(['vs/editor/editor.main'])`, Monaco is imported at build time via `import * as monaco from 'monaco-editor'` and bundled into the IIFE output by esbuild.

- **npm dependency**: Add `monaco-editor` (latest 0.54.x or newer) to `package.json` `dependencies`. This replaces the vendored `MonacoEditorComponent/monaco-editor/` directory and the legacy `install-dependencies.ps1` Monaco TGZ download (which was still pointing at v0.21.3).
- **Remove vendored Monaco**: Delete `MonacoEditorComponent/monaco-editor/` directory (the entire vendored distribution). Monaco now lives in `node_modules/monaco-editor/` and is consumed at build time only.
- **Keep `monaco.d.ts`**: The typings file (`monaco.d.ts`) is still needed for the C# type generation pipeline (`GenerateMonacoTypings/`). Copy it from `node_modules/monaco-editor/monaco.d.ts` during the build step, or update `GenerateMonacoTypings/package.json` to reference the same npm version.
- **Update `install-dependencies.ps1`**: Remove the Monaco TGZ download section. Replace with `npm install` (which now provides Monaco + esbuild + vscode-jsonrpc). Keep the webcomponents.js download if still needed, or move it to npm too. Add the esbuild build step.
- **Helpers import Monaco directly**: `import * as monaco from 'monaco-editor'` in TS helpers. esbuild resolves this from `node_modules/monaco-editor/esm/vs/editor/editor.main.js` (the `"module"` field in Monaco's package.json). No more `window.require` or AMD loader.

### Monaco worker bundling

Monaco workers (JSON, CSS, HTML, TypeScript, editor base) must be separate files — they're loaded via `new Worker()` at runtime. esbuild bundles each separately.

- **Worker entry points** (5 separate esbuild entries):
  - `monaco-editor/esm/vs/editor/editor.worker.js` → `editor.worker.js`
  - `monaco-editor/esm/vs/language/json/json.worker.js` → `json.worker.js`
  - `monaco-editor/esm/vs/language/css/css.worker.js` → `css.worker.js`
  - `monaco-editor/esm/vs/language/html/html.worker.js` → `html.worker.js`
  - `monaco-editor/esm/vs/language/typescript/ts.worker.js` → `ts.worker.js`
- **Worker output format**: IIFE (classic scripts — `new Worker()` without `{ type: 'module' }`)
- **Worker output path**: `WasmScripts/workers/` (for WASM EmbeddedResource) and copied to desktop content
- **`MonacoEnvironment.getWorkerUrl()`**: Configured in the entry point with runtime platform detection to resolve worker paths correctly for both WASM and desktop. On WASM, workers are loaded from the app's base URL. On desktop, from the virtual host root.

### esbuild configuration (`esbuild.config.mjs`)

Create a build script rather than a single CLI command — Monaco bundling requires multiple entry points and configuration:

- **Main bundle**: `ts-helpermethods/index.ts` → `WasmScripts/uno-monaco-helpers.js`
  - `--bundle --format=iife --target=es2015 --platform=browser --sourcemap=inline`
  - Monaco resolved from `node_modules/monaco-editor` ESM entry
  - `vscode-jsonrpc` resolved from `node_modules/vscode-jsonrpc/browser`
  - `--minify` for production builds
- **Worker bundles**: Each worker entry point → separate IIFE file
  - Same flags except no source maps needed for workers
- **IIFE rationale** (researched — mandatory, not a preference): (1) Uno Wasm Bootstrap loads `WasmScripts/` via RequireJS — ESM output breaks this pipeline; `[JSImport("globalThis.*")]` requires functions on `globalThis`. (2) On macOS/Linux, Uno's `SetVirtualHostNameToFolderMapping` falls back to `file://` URLs — `<script type="module">` fails over `file://` due to CORS (WHATWG spec). IIFE with classic `<script>` tags works universally.
- **Do NOT use esbuild's `globalName` option**: Use explicit `globalThis.functionName = functionName` assignments in `index.ts`.
- **Source maps**: `--sourcemap=inline` for main bundle. External `.map` files are unreliable with virtual host mapping.

### ESM migration of TS helpers

- **tsconfig.json update**: `"module": "ESNext"`, `"moduleResolution": "bundler"`, remove `outFile`, add `"noEmit": true` (esbuild does the emitting). Keep `"target": "ES2015"`, `"lib": ["ES2015", "DOM"]`.
- **npm dependencies**: `npm install monaco-editor vscode-jsonrpc esbuild` (add to `package.json`)
- **Entry point** (`ts-helpermethods/index.ts`): Imports all helper modules + bridge setup. Imports Monaco (`import * as monaco from 'monaco-editor'`). Assigns public functions to `globalThis` for `[JSImport]` compatibility. Configures `MonacoEnvironment.getWorkerUrl()`. This replaces both `tsc --outFile` concatenation and the AMD `require()` loading.
- **TS helpers migration**: Convert existing `ts-helpermethods/*.ts` from global scripts to ES modules (`import`/`export`). All functions referenced by `[JSImport]` or `InvokeScriptAsync` must be assigned to `globalThis` in the entry point. Trace the actual call graph from C# `[JSImport]` attributes to identify which functions need global exposure.
- **Monaco references**: Replace all `(<any>window).require.config(...)` / `(<any>window).require(['vs/editor/editor.main'], ...)` patterns with direct ESM imports. The `monaco` namespace is available as `import * as monaco from 'monaco-editor'` — esbuild resolves it at bundle time.

### Bridge protocol contract (single source of truth)
Define `bridge-protocol.md` documenting the JSON-RPC 2.0 protocol over WebView2 postMessage:

- **Wire format**: Standard JSON-RPC 2.0 (`{ "jsonrpc": "2.0", "method": "...", "params": {...}, "id": N }`). StreamJsonRpc + `SystemTextJsonFormatter` on C# side, `vscode-jsonrpc` on JS side.
- **Transport binding**: JS sends via `postWebViewMessage()` (cross-platform: `chrome.webview.postMessage` on Windows, `webkit.messageHandlers.unoWebView.postMessage` on macOS/Linux). C# sends via `ICodeEditorPresenter.PostWebMessage(string json)` (presenter-level abstraction — decouples transport from `CoreWebView2` internals). Both sides receive via their respective message event handlers.
- **Initialization handshake**: After Monaco loads, JS calls `jsonRpc.notify("editor/ready", { protocolVersion: 1 })`. C# validates version. If mismatched, log error and reject.

- **JS→C# methods** (JSON-RPC notifications and requests):
  - `editor/ready` (notification): `{ protocolVersion: 1 }` — Monaco init complete
  - `parentAccessor/setValue` (notification): `{ name, value }` — property change from JS
  - `parentAccessor/setValueWithType` (notification): `{ name, value, typeName }`
  - `parentAccessor/callAction` (notification): `{ name }`
  - `parentAccessor/callActionWithParameters` (notification): `{ name, parameters }`
  - `parentAccessor/callEvent` (request): `{ name, parameters }` → returns result string
  - `parentAccessor/getJsonValue` (request): `{ name }` → returns JSON string
  - `theme/getProperty` (request): `{ name: "currentThemeName"|"isHighContrast" }` → returns value
  - `debug/log` (notification): `{ level, message }`
  - `keyboard/keyDown` (notification): `{ event: <keyEventJson> }`

- **Typed parameter contracts** (shared between JS and C#, defined in this doc):
  - `SetValueParams { string name; JsonElement value; }`
  - `SetValueWithTypeParams { string name; JsonElement value; string typeName; }`
  - `CallActionParams { string name; }`
  - `CallActionWithParametersParams { string name; JsonElement parameters; }` (structured JSON — array, object, or primitive)
  - `CallEventParams { string name; JsonElement parameters; }` → returns `string?`
  - `GetJsonValueParams { string name; }` → returns `string`
  - `GetThemePropertyParams { string name; }` → returns `string`
  - `LogParams { string level; string message; }`
  - `KeyDownParams { JsonElement @event; }`
  - `EditorReadyParams { int protocolVersion; }`
  - Task 5 C# methods MUST use these exact shapes. Task 4 JS calls MUST use matching object params.

- **C#→JS methods** (JSON-RPC notifications and requests sent from C# to JS):
  - `editor/lifecycleUpdate` (notification): `{ loading: N, loaded: N }` — lifecycle event counts pushed to JS for testability (JS handler writes to `document.body.dataset.lifecycleLoaded`)
  - `editor/getValue` (request): `{}` → returns current editor text (JS handler calls `editor.getValue()`)
  - `editor/updateOptions` (notification): `{ options }` — push updated editor options to Monaco
  - Additional C#→JS methods may be added as needed during implementation; they MUST be documented in this protocol spec.
  - Most C#→JS calls continue to use `InvokeScriptAsync` (eval-style) for WASM compatibility

- **Request/response**: Handled by JSON-RPC 2.0 spec (`id` field). StreamJsonRpc manages correlation, timeouts, and error propagation automatically.
- **Timeout**: StreamJsonRpc default per-request timeout (configurable, default 5000ms).
- **Cleanup**: `JsonRpc.Dispose()` rejects all pending requests. Called on presenter disposal/navigation.
- **Cancellation**: `$/cancelRequest` supported natively by StreamJsonRpc.
- **Security considerations** (document in protocol spec, enforced by Task 5's `WebView2JsonRpcMessageHandler`):
  - **Requests/notifications** (messages with `method` field): Validate method name against known allowlist; validate required params per method; drop unknown methods with warning log
  - **Responses** (messages with `id` + `result`/`error`, no `method`): Validate payload structure (must have `id`); StreamJsonRpc handles correlation (unknown IDs safely ignored)
  - Maximum payload size: 10MB per message (all envelope types)
  - Future: capabilities negotiation in `editor/ready` response

### JS JSON-RPC client via `vscode-jsonrpc`
Use the `vscode-jsonrpc` npm package — the same JSON-RPC 2.0 library that Monaco/VS Code uses. Wire-compatible with StreamJsonRpc by design.

Create `ts-helpermethods/bridge/jsonRpcBridge.ts`:
- Import from `vscode-jsonrpc/browser` (NOT the Node.js entry point): `createMessageConnection`, `AbstractMessageReader`, `AbstractMessageWriter`, `MessageConnection`. esbuild must use `--platform=browser` to resolve this correctly.
- Custom `WebViewMessageReader extends AbstractMessageReader`: listens on `window.addEventListener('message', ...)`, fires callback with parsed JSON
- Custom `WebViewMessageWriter extends AbstractMessageWriter`: sends via platform-specific `postWebViewMessage()` (`chrome.webview.postMessage` on Windows, `webkit.messageHandlers.unoWebView.postMessage` on macOS/Linux)
- Creates `MessageConnection` and assigns to `window.__jsonRpc`
- Connection API used by Task 4: `connection.sendNotification(method, params)`, `connection.sendRequest(method, params)`, `connection.onRequest(method, handler)`, `connection.onNotification(method, handler)`, `connection.dispose()`

### HTML host page
- Container `<div id="editor-container">`
- **All `<script>` tags must be classic** (no `type="module"`) — `file://` CORS on macOS/Linux blocks ES modules
- Load order: Single `<script src="uno-monaco-helpers.js">` (IIFE, contains Monaco + helpers + bridge). No separate `loader.js` needed — Monaco is bundled.
- Bridge auto-inits on desktop (environment detection)
- After Monaco loads: `connection.sendNotification("editor/ready", { protocolVersion: 1 })`

### Resource packaging (explicit MSBuild targets)
- `<EmbeddedResource>` for WASM: `WasmScripts/uno-monaco-helpers.js` + `WasmScripts/workers/*.worker.js`
- `<Content CopyToOutputDirectory="PreserveNewest" />` for desktop: `DesktopContent/editor.html`, all JS bundles
- Single `uno-monaco-helpers.js` (esbuild output, same EmbeddedResource path as before) — loaded on both platforms
- Worker files: 5 separate `.worker.js` files in output
- Build-time existence check for all bundled JS files
- **Removed**: Vendored `monaco-editor/min/vs/**` tree. Monaco is now bundled into `uno-monaco-helpers.js`.

## Acceptance

- [ ] `bridge-protocol.md` documents JSON-RPC 2.0 method names, param schemas, transport binding, timeout, cleanup
- [ ] Protocol version included in `editor/ready` notification (`protocolVersion: 1`)
- [ ] Security constraints documented (method validation, required params, payload size limit)
- [ ] Protocol references StreamJsonRpc + `SystemTextJsonFormatter` (C#) and `vscode-jsonrpc` (JS) as implementations
- [ ] `monaco-editor`, `vscode-jsonrpc`, and `esbuild` npm dependencies added to `package.json`
- [ ] Vendored `MonacoEditorComponent/monaco-editor/` directory removed (Monaco consumed from `node_modules/`)
- [ ] `install-dependencies.ps1` updated: Monaco TGZ download removed, replaced with `npm install` + esbuild build
- [ ] `jsonRpcBridge.ts` creates `MessageConnection` with custom `WebViewMessageReader`/`WebViewMessageWriter`
- [ ] `window.__jsonRpc` exposes `MessageConnection` (`sendNotification`, `sendRequest`, `onRequest`, `onNotification`, `dispose`)
- [ ] JSON-RPC client wire-compatible with StreamJsonRpc (verified by unit test in Task 6)
- [ ] `tsconfig.json` updated: `"module": "ESNext"`, `"moduleResolution": "bundler"`, `outFile` removed, `"noEmit": true`
- [ ] All `ts-helpermethods/*.ts` converted to ES modules (`import`/`export`)
- [ ] `ts-helpermethods/index.ts` entry point imports Monaco from `monaco-editor` ESM, configures `MonacoEnvironment.getWorkerUrl()`, assigns public functions to `globalThis`
- [ ] No AMD `require()` or `loader.js` references remain — Monaco loaded via ESM import at build time
- [ ] `esbuild.config.mjs` produces main IIFE bundle + 5 worker IIFE bundles
- [ ] Main bundle: `WasmScripts/uno-monaco-helpers.js` (IIFE, `--platform=browser`, `--sourcemap=inline`)
- [ ] Worker bundles: `editor.worker.js`, `json.worker.js`, `css.worker.js`, `html.worker.js`, `ts.worker.js` (IIFE)
- [ ] No `<script type="module">` in `editor.html` — all classic `<script>` tags (file:// CORS safety)
- [ ] Public functions assigned to `globalThis` in entry point (not via esbuild `globalName`) — verified by `[JSImport("globalThis.*")]` compatibility
- [ ] Container div `id="editor-container"`
- [ ] Explicit MSBuild target for desktop content packaging with build-time existence check
- [ ] All desktop resources in build output (editor.html, uno-monaco-helpers.js, worker files)
- [ ] WASM EmbeddedResource items not broken (main bundle + workers)
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop` verified
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm` verified
