# fn-10-fix-reverted-emitter-docs-enable-cdp-ci.3 Enable Desktop CDP tests on Windows CI runner

## Description
Enable Desktop CDP integration tests on the Windows CI runner. The tests were previously enabled (commit `2fdc2bf`) but re-excluded (commit `8d522af`) due to fixture timeout. Windows runner images (`windows-latest`) have Chrome 144, Edge 144, ChromeDriver, EdgeDriver, and WebView2 Runtime pre-installed.

**Size:** M
**Files:** `.github/workflows/ci.yml`, `MonacoEditorComponent.Tests/DesktopAppFixture.cs`, `AGENTS.md`

## Prior failure analysis (from CI run 21957402273)

The previous attempt failed with `TimeoutException: Timeout 15000ms exceeded` at `DesktopAppFixture.cs:111` (`Page.WaitForFunctionAsync`). Key observations:

1. **WebView2 DID launch** — failure was at `WaitForFunctionAsync` (line 111), NOT at `WaitForCdpReady` (line 94) or `FindMonacoPage` (line 100). CDP connected and a page was found.
2. **Monaco didn't become ready within 15s** — the JS expression `typeof monaco !== 'undefined' && monaco.editor.getEditors().length > 0` never became true.
3. **Config mismatch**: The CI pre-builds the test app with `-c Release` (ci.yml line 165), but the fixture launches with `dotnet run --project ... -f net10.0-desktop` **without** `-c Release`. This triggers a Debug rebuild inside the fixture process, eating significant time from the timeout budget.
4. **Timeline**: 177 unit tests passed in 7s, then the fixture spent ~42s initializing before all 13 CDP tests failed with the same fixture InitializeAsync timeout.

## Approach

### Phase 1: Research & diagnose (investigate before changing)
- Examine the process log from the prior failure (if available in test-artifacts)
- Determine: did `dotnet run` rebuild in Debug mode? (would show in stdout log)
- Determine: did the page actually load content or was it a blank/error page?
- Check if the init race condition (fn-6.2) could prevent Monaco from ever becoming ready
- Research whether GH Actions Windows runners have a display session or are headless
- Consider other blockers: WebView2 virtual host mapping, file access permissions, etc.

### Phase 2: Fix based on findings
Potential fixes depending on root cause:
- **Config mismatch fix**: Pass `-c Release` to the fixture's `dotnet run` command OR launch the pre-built binary directly instead of using `dotnet run`
- **Timeout increase**: If Monaco is just slow on CI, increase `MonacoReadyTimeoutMs` from 15s to 30-60s with rationale comment
- **Init race workaround**: If fn-6.2's dual init path prevents Monaco from ever loading, may need a targeted workaround or to defer this task until fn-6.2 is complete
- **Other**: Whatever investigation reveals

### Phase 3: Enable and verify
- Remove `--filter-not-trait "Category=DesktopCDP"` from the Windows desktop-tests job (`.github/workflows/ci.yml` line 185)
- Keep the exclusion on Ubuntu (no WebView2) and macOS ARM (no WebView2 on macOS)
- Push and monitor via `gh pr checks --watch`
- If tests fail again, investigate the new failure before retrying

### Phase 4: Update docs
- Update AGENTS.md "Known CI limitations" section (lines 99-102) to reflect that DesktopCDP now runs on Windows
- Update the CI job structure table (lines 90-95) to include Desktop CDP in the Windows job description

## Key context
- `DesktopAppFixture` at `MonacoEditorComponent.Tests/DesktopAppFixture.cs` launches the test app with `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port={port}` and polls `/json/version`
- The fixture uses `WEBVIEW2_USER_DATA_FOLDER` for test isolation
- CI run 21957402273 (commit 9da971b) has the actual failure logs
- The previous agent concluded "headless environment can't fully start the desktop app" — **this was incorrect**. WebView2 started fine; Monaco initialization timed out.
- The init race condition (fn-6.2) where both `WebView_NavigationCompleted` and `CodeEditorLoaded` set `_initialized=true` may contribute to Monaco not loading
- Memory note: "Desktop CDP tests timeout on headless CI (need GUI runner for WebView2)" — **update this note** after investigation

## Acceptance
- [ ] Root cause of prior timeout understood and documented
- [ ] `--filter-not-trait "Category=DesktopCDP"` removed from Windows job in ci.yml
- [ ] Desktop CDP tests pass on Windows CI runner (verify via `gh pr checks --watch`)
- [ ] DesktopCDP exclusion preserved on Ubuntu and macOS ARM jobs
- [ ] AGENTS.md "Known CI limitations" updated to reflect CDP test enablement on Windows
- [ ] AGENTS.md CI job structure table updated for Windows job description
- [ ] If fixture timeout is the issue, timeout constants increased with rationale comment
- [ ] If config mismatch is the issue, fixture updated to use correct configuration
- [ ] Memory note updated with corrected CI finding
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
