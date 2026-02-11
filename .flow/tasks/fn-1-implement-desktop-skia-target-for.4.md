## Description

Modify JS/TS helpers for dual-mode operation (WASM JSExport + desktop JSON-RPC). Task 3 migrates all TS to ESM + esbuild and bundles Monaco from its ESM distribution (replacing AMD loader); Task 4 builds on that to integrate the JSON-RPC bridge into each helper. Task 4 owns the JS-side bridge integration including emitting `editor/ready`. All method names and param schemas follow Task 3's `bridge-protocol.md` verbatim. Note: Monaco is now available as `import * as monaco from 'monaco-editor'` — no AMD `require()` calls remain after Task 3.

**Size:** L (cross-cutting async transition — treat as high-risk M)
**Files:** MonacoEditorComponent/ts-helpermethods/asyncCallbackHelpers.ts, MonacoEditorComponent/ts-helpermethods/Monaco.Helpers.ParentAccessor.ts, MonacoEditorComponent/ts-helpermethods/Monaco.Helpers.ThemeAccessor.ts, MonacoEditorComponent/ts-helpermethods/otherScriptsToBeOrganized.ts, MonacoEditorComponent/ts-helpermethods/register*.ts

## Approach

- **Pre-step: verify actual call graph**: Before migrating, trace the actual TS call paths from `createMonacoEditor()` through `initializeMonacoEditor()`, `getParentValue`, `getParentJsonValue`, `getThemeCurrentThemeName`, `getThemeIsHighContrast`, `updateOptions`, and all `register*.ts` callbacks. Document which paths are live vs dead code. Migrate only live paths.

- **Environment detection**: In `createMonacoEditor()` at `asyncCallbackHelpers.ts`, detect WASM (`window.Module`) vs desktop (`window.chrome?.webview` or `window.webkit?.messageHandlers?.unoWebView`). Module-level flag.

- **JSON-RPC bridge initialization** (desktop only): Import the `MessageConnection` from the bridge module (`import { getConnection } from './bridge/jsonRpcBridge'`). ESM imports resolved by esbuild at bundle time. The connection is auto-initialized on desktop (created by Task 3's bridge code). Also exposed on `window.__jsonRpc` for external access (Playwright tests). This becomes the single communication channel for all JS→C# and C#→JS desktop messaging.

- **JS→C# via JSON-RPC** (replaces custom postMessage routing, uses `vscode-jsonrpc` `MessageConnection` API):
  - Property changes: `connection.sendNotification("parentAccessor/setValue", { name, value })` replaces `postWebViewMessage({ type: "setValue", ... })`
  - Action calls: `connection.sendNotification("parentAccessor/callAction", { name })` replaces `postWebViewMessage({ type: "callAction", ... })`
  - Event calls: `await connection.sendRequest("parentAccessor/callEvent", { name, parameters })` replaces custom request/response
  - Value reads: `await connection.sendRequest("parentAccessor/getJsonValue", { name })` replaces custom `resolveRequest` pattern
  - Theme queries: `await connection.sendRequest("theme/getProperty", { name })` replaces custom getThemeProperty routing
  - Debug logging: `connection.sendNotification("debug/log", { level, message })` replaces `postWebViewMessage({ type: "log", ... })`
  - Keyboard events: `connection.sendNotification("keyboard/keyDown", { event })` replaces `postWebViewMessage({ type: "keyDown", ... })`

- **C#→JS via JSON-RPC handlers** (desktop only): Register handlers using `vscode-jsonrpc` `MessageConnection` API:
  - `connection.onRequest("editor/getValue", () => editor.getValue())`
  - `connection.onNotification("editor/updateOptions", (params) => editor.updateOptions(params))`
  - `connection.onNotification("editor/lifecycleUpdate", (params) => { document.body.dataset.lifecycleLoading = params.loading; document.body.dataset.lifecycleLoaded = params.loaded; })` — exposes lifecycle counts to DOM for Playwright testability (Task 8)
  - Additional handlers as needed for C#→JS operations that currently use `InvokeScriptAsync`

- **Ready emit**: After Monaco loads and `initializeMonacoEditor()` completes, desktop posts `connection.sendNotification("editor/ready", { protocolVersion: 1 })`.

- **Request/response**: JSON-RPC handles all correlation via `id` field. No manual `requestId`/`resolveRequest` pattern needed. `vscode-jsonrpc` handles pending request tracking and timeouts. WASM continues using synchronous JSExport.

### Sync→async migration (from verified call graph)
- `ParentAccessor.getJsonValue(name)` — sync JSExport → `await jsonRpc.request("parentAccessor/getJsonValue", { name })` on desktop
- `getParentValue()` / `getParentJsonValue()` — sync callers → must become async
- `getThemeCurrentThemeName()` / `getThemeIsHighContrast()` — sync JSExport → `await jsonRpc.request("theme/getProperty", { name })` on desktop
- `initializeMonacoEditor()` — sync body → must await property reads and theme queries on desktop
- `updateOptions()` — sync getParentValue → must become async
- Language provider callbacks in `register*.ts` — already async, no changes expected

### ThemeAccessor dual-mode
- `setup()` must detect environment and use appropriate transport
- On desktop, `getCurrentThemeName()` and `getIsHighContrast()` route through JSON-RPC requests

### Sanitize/Desanitize handling
- WASM: Keep existing sanitize/desanitize encoding
- Desktop: Skip — values arrive as clean JSON via JSON-RPC
- Environment flag controls which path is taken
- CRITICAL: Task 5 must also skip `Desanitize()` on C# side for desktop

### JS-side cleanup
- Clear `EditorContext._editors` map on dispose
- Call `jsonRpc.dispose()` which rejects all pending JSON-RPC requests and removes event listeners

## Acceptance

- [ ] Actual TS call graph verified and documented before migration
- [ ] JS detects WASM vs desktop at startup
- [ ] WASM path unchanged (no regression)
- [ ] Desktop uses JSON-RPC via `vscode-jsonrpc` `MessageConnection` (`window.__jsonRpc`) for all bridge communication
- [ ] `editor/ready` notification emitted after Monaco init with `protocolVersion: 1`
- [ ] `getJsonValue` uses `jsonRpc.request("parentAccessor/getJsonValue", ...)` on desktop
- [ ] ThemeAccessor methods use JSON-RPC requests on desktop
- [ ] All live callers adapted (verified against actual call graph, not stale matrix)
- [ ] `initializeMonacoEditor()` awaits all async calls on desktop
- [ ] `connection.dispose()` called on cleanup (rejects pending, removes listeners)
- [ ] No custom `requestId`/`resolveRequest` pattern — JSON-RPC handles correlation
- [ ] `editor/lifecycleUpdate` notification handler registered — writes lifecycle counts to `document.body.dataset` (consumed by Task 8 tests)
- [ ] TypeScript compiles successfully

## Done summary
Implemented JS bridge dual-mode communication layer: added WASM/desktop environment detection, migrated ParentAccessor and ThemeListener to dual-mode (JSExport on WASM, JSON-RPC on desktop), made initializeMonacoEditor async for desktop property reads with timeouts, registered C#->JS JSON-RPC handlers (editor/getValue, editor/updateOptions, editor/lifecycleUpdate), emitted editor/ready notification after init, skipped sanitize/desanitize on desktop, and added deterministic dispose with reference-counted connection lifecycle.
## Evidence
- Commits: b257659, 453b42e, a696fe1, 92481d1
- Tests: npx tsc --project MonacoEditorComponent/tsconfig.json --noEmit, npm run build, dotnet build MonacoEditorComponent.slnx --no-restore
- PRs: