# fn-1-implement-desktop-skia-target-for.8 Playwright integration tests for desktop and WASM

## Description
Create Playwright-based integration tests that automate the desktop WebView2 content via CDP and the WASM target via standard browser automation. This provides automated end-to-end verification that the Monaco editor loads, responds to commands, and renders correctly.

**Size:** M
**Files:** MonacoEditorComponent.Tests/PlaywrightFixture.cs (new), MonacoEditorComponent.Tests/DesktopIntegrationTests.cs (new), MonacoEditorComponent.Tests/WasmIntegrationTests.cs (new), MonacoEditorComponent.Tests/PlaywrightSetup.cs (new)

## Approach

### Desktop integration tests (Windows only — Playwright CDP)

- **CDP connection**: WebView2 on Windows is Chromium-based. Set `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=<port>` as environment variable before launching the desktop app. This enables Chrome DevTools Protocol without modifying production code.
- **Deterministic app readiness signaling**: The fixture MUST NOT rely on arbitrary delays. Use this sequence:
  1. Launch app process, capture stdout/stderr to log file in `test-artifacts/`
  2. Poll `http://localhost:<port>/json/version` with 500ms interval, 30s timeout — this confirms CDP is ready
  3. Connect Playwright via `ConnectOverCDPAsync`
  4. Find Monaco page: loop `browser.Contexts[0].Pages` checking `page.Url` for the editor URL, with 10s timeout
  5. Wait for Monaco ready: `page.WaitForFunctionAsync("() => typeof monaco !== 'undefined' && monaco.editor.getEditors().length > 0", new() { Timeout = 15000 })`
  6. If any step times out, capture process stdout/stderr, throw with diagnostic message
- **Test fixture**: Create a `DesktopAppFixture` (xUnit `IAsyncLifetime`) that:
  1. Picks a random available port for CDP (bind to port 0, read assigned port)
  2. Sets `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` and `WEBVIEW2_USER_DATA_FOLDER` (unique per test run to prevent interference)
  3. Launches `MonacoEditorTestApp` desktop via `Process.Start` with `dotnet run --project ... -f net10.0-desktop`
  4. Follows the deterministic readiness sequence above
  5. On dispose: close browser connection, kill app process, clean up user data folder
- **Known pitfall**: Page detection can be non-deterministic. Use retry loop with timeout for finding the correct page. See Playwright WebView2 docs.

### Testability constraints and approach

**Critical constraint**: Playwright CDP connects to the WebView2 content only. It **cannot** see or interact with native Uno/XAML controls (buttons, TextBoxes, panels) surrounding the WebView2. All desktop test interactions must go through the WebView2 DOM.

**Testing approach** — verify through JS-side Monaco API and the JSON-RPC bridge:

- **Text round-trip**: Use `page.EvaluateAsync("() => monaco.editor.getEditors()[0].setValue('test text')")` to set text in Monaco directly, then read back via `page.EvaluateAsync("() => monaco.editor.getEditors()[0].getValue()")`. This tests the JS-side editor works. To test the JS→C# bridge pipeline, use `page.EvaluateAsync` to call `window.__jsonRpc.request("parentAccessor/getJsonValue", { name: "Text" })` and verify the C# bridge returns the current text value.
- **Theme switching**: Use `page.EvaluateAsync("() => monaco.editor.setTheme('vs-dark')")` to switch themes in Monaco, verify via `page.EvaluateAsync("() => monaco.editor.getEditors()[0]._themeService?.getColorTheme()?.themeName")`.
- **Decorations**: Add decorations via Monaco JS API, verify via `page.EvaluateAsync("() => monaco.editor.getEditors()[0].getModel().getAllDecorations().length")`.
- **Bridge round-trip** (JS→C#→JS): Use `page.EvaluateAsync` to send a JSON-RPC request (e.g., `window.__jsonRpc.request("parentAccessor/getJsonValue", { name: "Text" })`) and verify the C# side responds correctly through the bridge.
- **Lifecycle events (exactly-once assertion)**: Expose lifecycle event counts into the WebView2 DOM via the JSON-RPC bridge. C# `EditorLoading`/`EditorLoaded` handlers push lifecycle counts to JS via `JsonRpc.NotifyAsync("editor/lifecycleUpdate", { loading: N, loaded: N })`. JS handler writes to `document.body.dataset.lifecycleLoaded`. Playwright asserts `page.EvaluateAsync("() => document.body.dataset.lifecycleLoaded")` equals `"1"`. If count > 1, test fails.
- **Uno App MCP** (complementary, not automated): For verifying native Uno/XAML controls (property panels, status indicators), use the Uno App MCP for ad-hoc agent-driven testing. Not part of automated CI.

### Failure artifact collection

On any test failure:
- **Screenshot**: `page.ScreenshotAsync(new() { Path = $"test-artifacts/{testName}-failure.png" })` captured in test teardown
- **Process logs**: Capture app process stdout/stderr to `test-artifacts/{testName}-process.log`
- **Playwright traces**: Enable tracing with `context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true })`, save on failure with `context.Tracing.StopAsync(new() { Path = $"test-artifacts/{testName}-trace.zip" })`
- **Artifact directory**: `test-artifacts/` at repo root, add to `.gitignore`

### Desktop test cases

- Editor loads: Monaco instance created, `monaco.editor.getEditors().length > 0`
- Text round-trip: Set text via `page.EvaluateAsync` Monaco API → read back → verify match
- Bridge round-trip: Send JSON-RPC request via `page.EvaluateAsync` → verify response from C# bridge
- Theme switching: Switch theme via `page.EvaluateAsync` Monaco API → verify via JS theme query
- Decorations: Add decoration via `page.EvaluateAsync` Monaco API → verify via JS decoration count
- Lifecycle events: Assert `document.body.dataset.lifecycleLoaded === "1"` (exactly once, exposed via JSON-RPC bridge)

### WASM integration tests (Playwright browser)

- **Build precondition**: The WASM fixture must locate the test app build output. CI builds in `Release`, local dev typically uses `Debug`. The fixture should:
  - Check for `MonacoEditorTestApp/bin/Release/net10.0-browserwasm/wwwroot/` first
  - Fall back to `MonacoEditorTestApp/bin/Debug/net10.0-browserwasm/wwwroot/`
  - If neither exists, fail fast with: "WASM build output not found. Run `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm` first"
- **Serve**: Start a static file server on the resolved `wwwroot/` path.
- **Test fixture**: Create a `WasmAppFixture` that:
  1. Resolves WASM build output directory (Release → Debug fallback)
  2. Asserts directory exists (fail-fast with build instructions if missing)
  3. Starts a static file server on the resolved `wwwroot/` path
  4. Launches Playwright Chromium browser (headless)
  5. Navigates to `http://localhost:<port>/`
  6. Waits for Monaco ready using same `WaitForFunctionAsync` pattern as desktop
  7. On dispose: close browser, stop server
- **Test cases**: Editor loads, basic text editing, theme switching. Lighter coverage than desktop (WASM is already the established path).

### CI integration policy

- **Unit tests + WASM Playwright**: Run in CI on `ubuntu-latest`. CI uses `Release` configuration throughout. Task 6 adds CI steps: WASM build, Playwright install (`bin/Release/net10.0/playwright.ps1`), `dotnet test -c Release --filter "Category!=DesktopCDP"`.
- **Desktop CDP tests**: Require Windows runner with WebView2 runtime. Initially **manual gate only** — tagged `[Trait("Category", "DesktopCDP")]`, filtered out in CI. Must pass locally on Windows before merge.
- **Test filtering**: `dotnet test -c Release --filter "Category!=DesktopCDP"` for CI, `dotnet test` for full local run on Windows.

### Agent-driven testing pattern (documentation only)

- Document how an AI agent can use the Playwright MCP server with `--cdp-endpoint http://localhost:<port>` to connect to a running desktop app's WebView2 for ad-hoc verification.
- Pattern: `browser_snapshot` for accessibility tree, `browser_evaluate` for JS assertions, `browser_click` for interaction testing.
- This is a development convenience, not automated CI. Document in a comment block in the test fixture.

## Key context

- Playwright .NET API: `await playwright.Chromium.ConnectOverCDPAsync(endpointURL, options)` returns `Browser`
- WebView2 CDP requires `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` env var (NOT a code change to the presenter)
- Each test run needs unique `WEBVIEW2_USER_DATA_FOLDER` to prevent parallel interference
- macOS (WKWebView) and Linux (WebKitGTK) do NOT support CDP — these tests are Windows-only by design
- Playwright browser install: `pwsh bin/Release/net10.0/playwright.ps1 install chromium` (CI uses Release; local uses Debug path)
- If desktop app fails to start or CDP is unreachable, tests should fail fast with clear error (not hang)
- WASM output path: `MonacoEditorTestApp/bin/{Release|Debug}/net10.0-browserwasm/wwwroot/` — fixture resolves both configs

## Acceptance
- [ ] Desktop test fixture launches MonacoEditorTestApp with CDP enabled
- [ ] Deterministic readiness: fixture uses CDP version poll + Monaco WaitForFunction (no arbitrary sleeps)
- [ ] Playwright connects via `ConnectOverCDPAsync` and finds Monaco page
- [ ] Desktop tests pass: editor loads, text round-trip, bridge round-trip, theme switch, decorations, lifecycle
- [ ] All desktop test interactions go through WebView2 DOM only (no native Uno/XAML control interaction — Playwright CDP cannot see them)
- [ ] Bridge round-trip verified: JSON-RPC request via `page.EvaluateAsync` → C# responds → result verified
- [ ] Lifecycle exactly-once: counts exposed to WebView2 DOM via JSON-RPC bridge, Playwright asserts `lifecycleLoaded === "1"`
- [ ] Each test run uses unique CDP port and user data folder
- [ ] WASM fixture resolves build output from Release then Debug (fail-fast if neither exists)
- [ ] WASM test fixture serves browserwasm output and connects Playwright browser
- [ ] WASM tests pass: editor loads, basic text editing, theme switch
- [ ] Tests fail fast with clear error when app fails to start or CDP unreachable
- [ ] Failure artifacts collected in `test-artifacts/`: screenshot + process logs + Playwright trace on failure
- [ ] `test-artifacts/` added to `.gitignore`
- [ ] Desktop CDP tests tagged with `[Trait("Category", "DesktopCDP")]` for CI filtering
- [ ] Playwright MCP agent-testing pattern documented in code comments
- [ ] `dotnet test` runs all tests (unit + Playwright) successfully on Windows
- [ ] `dotnet test --filter "Category!=DesktopCDP"` runs unit + WASM tests on any OS

## Done summary

Added Playwright integration tests for desktop CDP and WASM targets with xUnit v3 fixtures, deterministic readiness, failure artifact collection, and CI filter fix for MTP2 runner.

## Done evidence

```json
{"commits": ["35e5b071ebe8111bc5b4a9faeb9c766b5f3bc37e", "c68e82db8c8e66a2a19e12fd18fd34f02cc90e9f", "9fbea14bfe8c70a2320e6ce61b8c394f04cd13c1"], "tests": ["dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj --filter-not-trait Category=DesktopCDP --filter-not-trait Category=WasmPlaywright", "dotnet build MonacoEditorComponent.slnx --no-restore"], "prs": []}
```
