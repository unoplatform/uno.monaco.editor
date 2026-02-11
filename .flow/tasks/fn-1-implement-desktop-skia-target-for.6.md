## Description
Create an xUnit v3 + MTP2 unit test project, extract testable pure helper logic into seams, add it to the solution, and write initial tests.

**Size:** M
**Files:** MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj (new), MonacoEditorComponent.Tests/*.cs (new), MonacoEditorComponent.slnx, MonacoEditorComponent/Properties/AssemblyInfo.cs (InternalsVisibleTo — avoids csproj contention with Task 3), .github/workflows/ci.yml

## Approach

- **Project setup**: Create `MonacoEditorComponent.Tests` targeting `net10.0`. Package: `xunit.v3.mtp-v2` (3.2.0). `<OutputType>Exe</OutputType>` (required for xUnit v3). Add `Microsoft.NET.Test.Sdk` (18.0.1). Add project reference to MonacoEditorComponent. Add `[InternalsVisibleTo]` attribute in `Properties/AssemblyInfo.cs` (not csproj — avoids merge contention with Task 3's csproj changes).
- **Verify global.json**: Confirm `"test": { "runner": "Microsoft.Testing.Platform" }` is present (added by Task 1). If missing, add it — but Task 1 is the primary owner.
- **Solution**: Add test project to `MonacoEditorComponent.slnx`.

- **Extract pure helper seams** (prerequisite for meaningful tests):
  - **Sanitize/Desanitize**: Extract from `ParentAccessor.wasm.cs` into a standalone internal utility class (e.g., `BridgeEncoding`). Note: source has typo `Santize` — rename to `Sanitize` during extraction. Critical edge case: `%` appears as both an encoded character AND the escape prefix (`%<charcode>`). Test that `%` itself survives round-trip without double-encoding.
  - **WebView2JsonRpcMessageHandler**: Test the custom `IJsonRpcMessageHandler` with in-memory `Channel<JsonRpcMessage>`. Verify: write serializes and calls mock sender, read deserializes incoming messages, disposal cancels reader. This is the core transport seam — testable without a real WebView2.
  - **JSON-RPC target dispatch**: Construct `JsonRpc` over in-process pipe (`Nerdbank.FullDuplexStream`), attach a mock bridge target with `[JsonRpcMethod]` attributes, send JSON-RPC messages, verify method dispatch and return values. Tests the wiring without any UI or WebView2.
  - **`vscode-jsonrpc` wire compatibility**: Write a test that constructs sample JSON-RPC 2.0 messages matching the `vscode-jsonrpc` output format (standard JSON-RPC 2.0), feeds them through `WebView2JsonRpcMessageHandler.Reader`, and verifies StreamJsonRpc (with `SystemTextJsonFormatter`) dispatches them correctly. This validates wire compatibility without running Node.js. Also verify that `SystemTextJsonFormatter` serialization is consistent with `vscode-jsonrpc` expectations (camelCase, no extra fields).
  - **URI normalization**: Extract `UriHelper.AbsoluteUriString` logic into testable static method.
  - **Navigation allowlist**: Test `DesktopCodeEditorPresenter.IsNavigationAllowed(string uri, string? allowedFileContentRoot)` — this is an `internal static` pure function added by Task 2. Test cases: `about:blank` allowed, `about:blank#fragment` allowed, `about:blankevil` blocked (URI-parsed, not prefix-matched), `https://uno-monaco.example/...` allowed, `https://evil.com/...` blocked, `https://uno-monaco.example:8080/...` blocked (non-default port), `file://` blocked when `allowedFileContentRoot` is null, `file:///path/to/content/foo.html` allowed when `allowedFileContentRoot` is `/path/to/content`, `file:///other/path` blocked, `file:///path/to/content/../secret` blocked (path traversal — `Path.GetFullPath` canonicalizes). On Windows tests use case-insensitive path comparison; on macOS/Linux use case-sensitive.
  <!-- Updated by plan-sync: fn-1-implement-desktop-skia-target-for.2 added IsNavigationAllowed as internal static testable seam -->
  <!-- Updated by plan-sync: fn-1-implement-desktop-skia-target-for.2 hardened about:blank check (URI parsing not StartsWith), file:// path check (Path.GetFullPath + OS-aware case comparison) -->
  - **Language-extension mapping**: If C#-side dictionary implemented in Task 5, test it here.
  - **RenderingBackend**: Verify enum values.

- **Write tests**: Tests for each extracted seam. Focus on edge cases (special characters in sanitize, malformed JSON in bridge parser, unknown message types, `%` round-trip, empty strings, null inputs).

- **`JsonElement` → `string[]` conversion tests**: Test the deterministic mapping rules from Task 5:
  - `JsonElement` array `["a","b"]` → `string[] { "a", "b" }` (element-wise `GetRawText()`)
  - `JsonElement` string `"single"` → `string[] { "single" }` (single-element)
  - `JsonElement` null / `JsonValueKind.Undefined` → `string[] { }` (empty array)
  - `JsonElement` object `{ "key": "val" }` → `string[] { "{\"key\":\"val\"}" }` (raw text, single-element)
  - Nested arrays and mixed types preserve JSON token fidelity

- **Playwright package**: Add `Microsoft.Playwright` NuGet to test project (used by Task 8). This avoids Task 8 needing to modify the csproj (file contention).

- **CI integration**: Update `.github/workflows/ci.yml`. CI builds in `Release` configuration — all test steps MUST use `-c Release` consistently:
  1. After solution build, add: `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm -c Release` (prerequisite for WASM Playwright tests in Task 8)
  2. Add: `pwsh MonacoEditorComponent.Tests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium` (install Playwright browser + OS deps — path uses `Release` to match build config)
  3. Add: `dotnet test MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj -c Release --filter "Category!=DesktopCDP" --logger "trx;LogFileName=test-results.trx"` (run tests excluding Windows-only desktop CDP tests)
  4. Add upload step: `actions/upload-artifact` for `test-artifacts/` on failure
  5. Initially runs unit tests only (Task 8 adds WASM fixture later, but WASM build output will already exist)

## Key context

- xUnit v3 MTP v2 requires `<OutputType>Exe</OutputType>`
- On .NET 10, configure test runner in `global.json` not MSBuild properties — Task 1 owns this config
- No existing Uno + xUnit v3 + MTP2 examples found — this is novel
- Mock presenter integration tests are NOT feasible: `CodeEditor.OnApplyTemplate()` uses `GetTemplateChild("View")` which requires a running XAML visual tree. Pure function tests only in this task.
- CI uses `Release` configuration — Playwright install script path is `bin/Release/net10.0/playwright.ps1`

## Acceptance
- [ ] Test project created with `xunit.v3.mtp-v2` and MTP2 runner
- [ ] Test project targets `net10.0` with `<OutputType>Exe</OutputType>`
- [ ] `global.json` MTP2 runner config verified present (Task 1 owns adding it)
- [ ] `Microsoft.Playwright` NuGet added to test project
- [ ] `dotnet test` runs successfully (local, Debug config)
- [ ] Pure helper seams extracted (bridge parser, sanitize/desanitize with rename, URI normalization)
- [ ] Sanitize `%` round-trip edge case explicitly tested
- [ ] `WebView2JsonRpcMessageHandler` tested with in-memory channels (write/read/disposal)
- [ ] JSON-RPC target dispatch tested via in-process pipe (method routing, return values)
- [ ] StreamJsonRpc + `SystemTextJsonFormatter` wire compatibility verified (sample JSON-RPC 2.0 messages in `vscode-jsonrpc` format fed through handler → method dispatch verified)
- [ ] `JsonElement` → `string[]` conversion edge cases tested (array, string, null, object inputs)
- [ ] `IsNavigationAllowed` edge cases tested (about:blank, allowed virtual host, blocked external https, file:// with/without content root, path traversal)
- [ ] Tests cover extracted seams with edge cases
- [ ] Project added to solution file
- [ ] CI workflow: WASM test app build step added (`-f net10.0-browserwasm -c Release`)
- [ ] CI workflow: Playwright browser + OS deps installed (`bin/Release/net10.0/playwright.ps1 install --with-deps chromium`)
- [ ] CI workflow: `dotnet test -c Release --filter "Category!=DesktopCDP"` step added
- [ ] CI workflow: `test-artifacts/` uploaded on test failure

## Done summary
Created xUnit v3 + MTP2 test project with 94 unit tests covering BridgeEncoding, WebView2JsonRpcMessageHandler, JsonRpc target dispatch, vscode-jsonrpc wire compatibility, IsNavigationAllowed, JsonElement conversion, RenderingBackend, and UriHelper. Updated CI with WASM build, Playwright install, test execution, and artifact upload steps.
## Evidence
- Commits: b8355f7, 4bd2281
- Tests: dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj (94 passed, 0 failed)
- PRs: