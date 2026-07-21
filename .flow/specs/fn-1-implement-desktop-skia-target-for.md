# Implement Desktop (Skia) Target for Monaco Editor

## Problem

The MonacoEditorComponent only works on WASM via `BrowserHtmlElement` and `JSImport`/`JSExport`. Desktop (Skia) builds render nothing because all presenter/bridge code is WASM-only. Desktop support is needed for Windows, macOS, and Linux.

## Key Context

### Architecture

- **Single `net10.0` TFM**: Library uses `<TargetFrameworks>net10.0</TargetFrameworks>` (plural property name — Uno requirement). `net9.0` dropped. NO `net10.0-desktop` in library.
- **Runtime detection only**: NO `#if __WASM__` / `#if __DESKTOP__` preprocessor directives. ALL platform branching via `OperatingSystem.IsBrowser()`.
- **File suffix convention**: `.wasm.cs`/`.desktop.cs` suffixes have NO compile-time meaning in a single-TFM library. Both compile into the same assembly. Used only as organizational convention.
- **JSImport/JSExport**: Compile in `net10.0`. Work only at runtime on WASM. Throw `PlatformNotSupportedException` on desktop.
- **BrowserHtmlElement**: Available on all TFMs via Uno.WinUI. Only functional on WASM. Safe as long as never instantiated on desktop.
- **Two presenter types**: `WasmCodeEditorPresenter` and `DesktopCodeEditorPresenter`. Runtime factory in `OnApplyTemplate()`.
- **Generic.xaml**: ContentPresenter placeholder. Factory creates correct presenter at runtime.

### Locked Architectural Decisions 

1. **Single TFM**: `<TargetFrameworks>net10.0</TargetFrameworks>`. No net9.0. No net10.0-desktop. Plural property name required by Uno.
2. **Runtime detection**: `OperatingSystem.IsBrowser()` everywhere. No preprocessor defines.
3. **Two presenter types**: `WasmCodeEditorPresenter` and `DesktopCodeEditorPresenter`. Both implement `ICodeEditorPresenter`. NOT partials of the same class.
4. **Generic.xaml + factory**: `<ContentPresenter x:Name="View" />` in XAML. `OnApplyTemplate()` creates correct presenter via `OperatingSystem.IsBrowser()`.
5. **Presenter contracts**:
   - **Script execution**: `Task<string> InvokeScriptAsync(string script)` on `ICodeEditorPresenter`. Return contract: always returns **raw JSON token** (string, number, object, null). WASM wraps `NativeMethods.InvokeJS()` output. Desktop wraps `CoreWebView2.ExecuteScriptAsync()` output (already JSON-encoded). Retained for WASM path and legacy eval-style calls on desktop.
   - **Message send**: `void PostWebMessage(string json)` on `ICodeEditorPresenter`. Desktop wraps `CoreWebView2.PostWebMessageAsJson()`. WASM: no-op or throws (not used). This is the transport-level send for the JSON-RPC writer — decouples `WebView2JsonRpcMessageHandler` from `CoreWebView2` internals. Uno's `WebView2` already abstracts Windows/macOS/Linux native views, so the presenter-level method works cross-platform.
   - **Inbound message event**: `event EventHandler<WebViewMessageEventArgs> MessageReceived` on `ICodeEditorPresenter`. On desktop, routes raw `WebMessageReceived` payloads. On WASM, not used (JSExport direct calls). Used by `WebView2JsonRpcMessageHandler` as the transport ingress for StreamJsonRpc.
6. **DispatcherQueue**: Migrate from `CoreDispatcher`. Locked.
7. **ESM authoring → IIFE output via esbuild (including Monaco)**: All TypeScript source uses ESM (`import`/`export`). Monaco Editor is consumed from its ESM distribution (`monaco-editor/esm/`) via `import * as monaco from 'monaco-editor'` — the AMD distribution is deprecated (v0.53.0+). esbuild replaces both `tsc --outFile` and the AMD loader — bundles Monaco ESM + all helpers + `vscode-jsonrpc` into a single `uno-monaco-helpers.js` in **IIFE format**. Monaco workers are bundled separately as individual IIFE files (`editor.worker.js`, `json.worker.js`, `css.worker.js`, `html.worker.js`, `ts.worker.js`). `MonacoEnvironment.getWorkerUrl()` configured to resolve worker paths per platform. IIFE is mandatory for two reasons: (1) Uno Wasm Bootstrap uses RequireJS to load `WasmScripts/` — ESM output breaks this pipeline, and `[JSImport("globalThis.*")]` requires functions on `globalThis`; (2) On macOS/Linux, Uno converts virtual host URLs to `file://` — `<script type="module">` fails due to CORS. esbuild config: `--format=iife --bundle --target=es2015 --platform=browser --sourcemap=inline`. Do NOT use esbuild's `globalName` — use explicit `globalThis` assignments. `tsconfig.json` updated to `"module": "ESNext"` with `"moduleResolution": "bundler"`, `outFile` removed, `"noEmit": true`. The vendored `monaco-editor/` directory and `install-dependencies.ps1` Monaco download are replaced by `npm install monaco-editor` in the project `package.json`.
8. **Bridge protocol**: JSON-RPC 2.0 via StreamJsonRpc (C#) + `vscode-jsonrpc` (JS). `bridge-protocol.md` documents method names, parameter schemas, and transport binding. Replaces custom `{ "type": ... }` message routing.
9. **StreamJsonRpc transport**: Custom `WebView2JsonRpcMessageHandler : IJsonRpcMessageHandler` (~50-100 lines). Writer sends via `ICodeEditorPresenter.PostWebMessage()` (presenter-level abstraction, not direct `CoreWebView2`). Reader feeds from `ICodeEditorPresenter.MessageReceived` into `Channel<JsonRpcMessage>`. Single `JsonRpc` instance per presenter; bridge classes register as RPC targets. **Must use `SystemTextJsonFormatter`** (not Newtonsoft) for AOT compatibility.
10. **JS-side JSON-RPC**: `vscode-jsonrpc` npm package — import from `vscode-jsonrpc/browser` (NOT the Node.js entry point). Same JSON-RPC 2.0 library that Monaco/VS Code uses. Custom `AbstractMessageReader`/`AbstractMessageWriter` for WebView2 `postMessage` transport. Bundled into the unified `uno-monaco-helpers.js` by esbuild with `platform: 'browser'` (tree-shaken, ~30KB contribution). Desktop helpers import the bridge connection directly via ESM (resolved at esbuild bundle time, not runtime). Exposes `MessageConnection` on `window.__jsonRpc` for external access (Playwright tests).
11. **Helper type abstractions**: Before creating desktop variants, extract interfaces (`IParentAccessor`, `IThemeListener`, etc.) or use base class pattern so `CodeEditor.Events.cs` can reference either variant uniformly.
12. **Unit tests**: xUnit v3 with MTP2. Test seams extracted as pure helpers before writing tests.
13. **API changes**: Breaking changes allowed if they unify WASM and desktop API surface. Prefer a single public API that works identically on both platforms.
14. **AOT compatibility**: All serialization must be AOT-friendly. Use `System.Text.Json` source generators (`[JsonSerializable]` context) for all DTO types. StreamJsonRpc configured with `SystemTextJsonFormatter`. No Newtonsoft.Json. No reflection-based serialization. Follow .NET 10 coding standards (file-scoped namespaces, primary constructors for DTOs, `required` modifier, collection expressions).

### Testing Strategy

Uno UITest does NOT work with Skia desktop targets. The testing strategy uses a layered approach:

**Layer 1 — Pure unit tests (xUnit v3 + MTP2)**:
- Extract `Sanitize`/`Desanitize` into standalone utility. Test round-trips including `%` self-encoding edge case.
- `WebView2JsonRpcMessageHandler`: test with in-memory `Channel<JsonRpcMessage>`. Verify round-trip serialization, disposal cleanup. StreamJsonRpc makes the transport independently testable.
- JSON-RPC target registration: construct `JsonRpc` over in-process pipe, attach mock target, verify method dispatch.
- `UriHelper.AbsoluteUriString`: various URI schemes and env var states.
- Argument serialization from `WebViewExtensions.cs:116-136`.
- Runs on all host OSes. No UI dependency.

**Layer 2 — Playwright CDP integration tests (Windows desktop)**:
- WebView2 on Windows is Chromium-based and supports Chrome DevTools Protocol.
- `DesktopCodeEditorPresenter` enables CDP via `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=<port>` environment variable (test-only, not baked into production code).
- Playwright .NET `ConnectOverCDPAsync("http://localhost:<port>")` connects to the running desktop app's WebView2.
- Tests: editor loads, text set/get round-trip, theme switching, decorations render, lifecycle events fire.
- **Windows only** — macOS (WKWebView) and Linux (WebKitGTK) are not Chromium and do not support CDP.
- Known pitfalls: parallel test interference (unique user data dir per run), page detection delays (loop until Monaco page found), no video recording via CDP.

**Layer 3 — Playwright browser tests (WASM regression)**:
- Build `net10.0-browserwasm` → serve static files → Playwright navigates and interacts.
- Tests: editor loads, basic text editing, theme switching.
- Ensures WASM is not regressed by desktop work.

**Layer 4 — Manual validation (macOS/Linux/comprehensive)**:
- Structured per-platform evidence matrix with commands, versions, results.
- Required for macOS and Linux where no automated path exists.
- Agent-driven Playwright MCP (`--cdp-endpoint`) available for Windows ad-hoc testing.

**Deferred — Uno RuntimeTests Engine**:
- Uno's `Uno.UI.RuntimeTests.Engine` runs in-app MSTest on all Skia platforms (including headless via `xvfb-run`).
- This is how Uno tests their own WebView2 (see `unoplatform/uno` repo `Given_WebView2.cs`).
- Deferred to future epic: adds MSTest alongside xUnit (two frameworks), increases MonacoEditorTestApp complexity.
- If macOS/Linux automated testing becomes required, revisit this approach.

**NOT feasible — Mock presenter unit tests**:
- `CodeEditor.OnApplyTemplate()` gets the presenter via `GetTemplateChild("View")` — a hard cast from the XAML visual tree.
- Cannot inject a mock presenter without a running Uno UI host or refactoring the control.
- If testable factory injection is added in a future refactor, mock tests become viable.

### Desktop Approach

- **WebView2**: Uno's `Microsoft.UI.Xaml.Controls.WebView2` (wraps WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux)
- **Resource serving**: `SetVirtualHostNameToFolderMapping()` — provides HTTPS origin on Windows (native WebView2), but Uno converts to `file://` URLs on macOS (WKWebView) and Linux (WebKitGTK). All script loading uses classic `<script>` tags (IIFE), never `<script type="module">`, to avoid `file://` CORS failures.
- **Bridge transport**: JSON-RPC 2.0 over `PostWebMessageAsJson()`/`WebMessageReceived`. StreamJsonRpc + `SystemTextJsonFormatter` on C# side, `vscode-jsonrpc` on JS side. Bidirectional: both sides can initiate requests and send notifications.
- **C#→JS** (legacy/eval): `CoreWebView2.ExecuteScriptAsync(script)` still available via `InvokeScriptAsync` for WASM-compat callers
- **C#→JS** (JSON-RPC): `JsonRpc.NotifyAsync("method", params)` or `JsonRpc.InvokeAsync<T>("method", params)` — routed through `PostWebMessageAsJson`
- **JS→C#** (JSON-RPC): `connection.sendNotification("method", params)` or `await connection.sendRequest("method", params)` — routed through `postWebViewMessage()` → `WebMessageReceived` → StreamJsonRpc reader
- **NuGet**: `StreamJsonRpc` added to `MonacoEditorComponent.csproj` (Task 2). AOT-safe: configured with `SystemTextJsonFormatter` + `[JsonSerializable]` source-generated context.
- **npm**: `monaco-editor` (ESM distribution, replaces vendored AMD), `vscode-jsonrpc`, `esbuild` — all bundled into unified IIFE output (Task 3). Replaces the legacy `install-dependencies.ps1` Monaco download.

### Reference Projects

- **celbridge-org/celbridge**: Uno + Monaco + WebView2 + virtual host mapping
- **microsoft/PowerToys**: Monaco + WebView2 + `SetVirtualHostNameToFolderMapping`
- **lk-code/winui.monaco-editor**: WinUI 3 + WebView2 + Monaco AMD + cross-platform postMessage
- **unoplatform/uno** `Given_WebView2.cs`: Uno's own WebView2 runtime tests (RuntimeTests Engine pattern)
- **nicoriff/tauri-playwright-cdp-test**: Playwright CDP testing for desktop WebView apps

## Quick commands

```bash
dotnet build MonacoEditorComponent.slnx --no-restore
dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop
dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm
dotnet test MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj
dotnet test MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj --filter "Category!=DesktopCDP"
dotnet run --project MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop
```

### CI gating policy

- **CI runs on `ubuntu-latest`**: Unit tests + WASM Playwright tests run automatically (`dotnet test --filter "Category!=DesktopCDP"`).
- **Desktop CDP tests are Windows-only, manual gate**: Tagged `[Trait("Category", "DesktopCDP")]`. Run on windows runner
- **Failure artifacts**: CI should upload `test-artifacts/` directory on test failure (screenshots, process logs, Playwright traces).
- **No `#if __WASM__` / `#if __DESKTOP__` closure**: The grep check for preprocessor directives applies to `MonacoEditorComponent/` only. `MonacoEditorTestApp/` may retain platform-specific `#if` in `App.xaml.cs` and platform startup files as Uno standard practice.

## Acceptance

- [ ] Library targets single `net10.0` in `TargetFrameworks` (plural)
- [ ] No `#if __WASM__` or `#if __DESKTOP__` preprocessor directives in `MonacoEditorComponent/` (verified by grep)
- [ ] Monaco editor renders and is interactive on Windows desktop (Skia)
- [ ] Monaco editor renders and is interactive on macOS desktop (Skia)
- [ ] Monaco editor renders and is interactive on Linux desktop (Skia/X11) — **deferred: no Linux env available (see Known Gaps)**
- [ ] Core features pass on Windows and macOS: text editing, themes, keyboard, decorations, markers (Linux deferred)
- [ ] All language services work on Windows: CodeLens, Hover, Completion, Color providers
- [ ] Language services verified on macOS (failures documented); Linux deferred
- [ ] Multiple editor instances work (TabView or equivalent)
- [ ] WASM functionality not regressed
- [ ] `RenderingBackend` property exposed
- [ ] `IsEditorLoadedProperty` DP type corrected to `bool`
- [ ] Lifecycle events fire exactly once on both platforms
- [ ] Unit test project with xunit3 + MTP2 passes
- [ ] `global.json` has MTP2 runner config
- [ ] Playwright CDP integration tests pass on Windows desktop and Windows CI runners
- [ ] Playwright browser tests pass for WASM regression
- [ ] CI runs unit, WASM + desktop tests 
- [ ] Per-platform pass/fail matrix documented with evidence (commands, versions, results, failure artifacts)

### Known Gaps (future epics)

- **Linux desktop validation**: No Linux runner or dev machine available during this epic. Linux uses WebKitGTK (Uno Skia) — same `net10.0` TFM builds, but WebKitGTK has known differences from Chromium WebView2. The architecture supports Linux (single TFM, runtime detection, no preprocessor directives), but manual validation is deferred until a Linux environment is available. Acceptance criteria referencing "all three platforms" and "Linux" are deferred to the first session with Linux access.
- **C# typings pipeline**: Generated C# types in `Monaco/` were produced by TypedocConverter (now dead) from `monaco-editor@0.21.3` — 33 major versions behind the v0.54.0 runtime. Types use `Newtonsoft.Json` `[JsonProperty]` attributes. A future epic should: find/build a replacement generator, regenerate from current `.d.ts`, and migrate to `System.Text.Json` `[JsonPropertyName]` / source generators. This epic uses existing types as-is.

## References

- [Uno Platform-specific C#](https://platform.uno/docs/articles/platform-specific-csharp.html)
- [Uno WebView2 docs](https://platform.uno/docs/articles/controls/WebView.html)
- [Uno WebView2 API support](https://platform.uno/docs/articles/implemented/microsoft-ui-xaml-controls-webview2.html)
- [Uno Platform-specific XAML](https://platform.uno/docs/articles/platform-specific-xaml.html)
- [JSImport/JSExport interop](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop)
- [xUnit v3 MTP2 setup](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
- [Playwright .NET WebView2 docs](https://playwright.dev/dotnet/docs/webview2)
- [Playwright .NET ConnectOverCDPAsync API](https://playwright.dev/dotnet/docs/api/class-browsertype#browser-type-connect-over-cdp)
- [Playwright MCP server](https://github.com/microsoft/playwright-mcp)
- [Uno RuntimeTests Engine](https://github.com/unoplatform/uno.ui.runtimetests.engine)
- [celbridge Monaco+WebView2](https://github.com/celbridge-org/celbridge)
- [PowerToys MonacoEditorControl](https://github.com/microsoft/PowerToys)
- [winui.monaco-editor](https://github.com/lk-code/winui.monaco-editor)
- [Uno App MCP](https://platform.uno/docs/articles/features/using-the-uno-mcps.html)
- [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc)
- [StreamJsonRpc IJsonRpcMessageHandler](https://github.com/microsoft/vs-streamjsonrpc/blob/main/doc/extensibility.md)
- [StreamJsonRpc SystemTextJsonFormatter](https://github.com/microsoft/vs-streamjsonrpc/blob/main/doc/resiliency.md)
- [vscode-jsonrpc](https://www.npmjs.com/package/vscode-jsonrpc)
- [JSON-RPC 2.0 Specification](https://www.jsonrpc.org/specification)
