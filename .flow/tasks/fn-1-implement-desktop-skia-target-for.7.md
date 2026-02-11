## Description
Final validation pass across all platforms. Run automated tests (unit + Playwright), then perform manual validation on macOS and Linux where automated coverage does not exist. Produce structured per-platform evidence matrix.

**Size:** M (validation and evidence — bug fixes bounded)
**Files:** MonacoEditorTestApp/ (validation), MonacoEditorComponent/ (targeted bug fixes only)

## Approach

### Automated validation (run first)

Run the appropriate test command based on the platform being validated:

- **Windows**: `dotnet test MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj` (full suite including desktop CDP tests)
- **macOS/Linux**: `dotnet test MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj -- --filter-not-trait "Category=DesktopCDP" --filter-not-trait "Category=WasmPlaywright"` (excludes Windows-only desktop CDP tests and WASM Playwright tests that require Playwright browser install + WASM build output)
<!-- Updated by plan-sync: fn-1-implement-desktop-skia-target-for.8 uses xUnit v3 MTP2 runner syntax (--filter-not-trait) not MSTest-style --filter; WASM Playwright tests have separate WasmPlaywright trait -->

On macOS/Linux this runs pure helper tests (Task 6) only. Playwright integration tests (Task 8) are excluded and run separately on Windows (DesktopCDP) or when Playwright prerequisites are installed (WasmPlaywright).

Review results. Desktop CDP tests cover editor load, text round-trip, theme switch, decorations, lifecycle on Windows. WASM browser tests cover regression on all platforms.

**Fix any failures**: If automated tests reveal bugs, fix in MonacoEditorComponent and re-run. Bug fixes are limited to **root-cause fixes for test failures only** — no refactoring, no feature additions, no unrelated cleanup. If a fix requires more than ~50 lines of change, document as a follow-up issue rather than fixing inline.

### Manual validation (macOS and Linux)

macOS (WKWebView) and Linux (WebKitGTK) do not support CDP, so Playwright cannot automate them. Manual validation with structured evidence:

- **Core features — must pass all 3 platforms**: Text editing, theme switching, keyboard shortcuts, decorations, markers, editor resize, lifecycle events (exactly-once EditorLoading/EditorLoaded).
- **Language services — must pass Windows**: Completion, Hover, CodeLens, Color providers. Verify on macOS/Linux; document failures with platform, symptom, and workaround.
- **Multi-instance — smoke-test all 3 platforms**: Open/close tabs with multiple editors. Verify: no exceptions, no stuck pending requests, independent state.
- **Performance**: Typing responsive, smoke-test with file > 10K lines.

### WASM regression

- Automated: Playwright browser tests (Task 8) cover basic WASM regression.
- Manual: Verify on actual browser (not just headless) that editing/themes/providers work.

### Evidence requirements

Each platform test produces structured evidence:
1. **Command run**: exact `dotnet run` or build command (including `--filter` flag if applicable)
2. **Platform**: OS version, runtime identifier
3. **Test matrix**: table with Feature | Result (pass/fail/skip) | Notes
4. **Failure artifacts**: For any failure — process stdout/stderr logs, screenshots, and Playwright trace files (if applicable). All artifacts go to `test-artifacts/` at repo root (same directory used by Task 8 automated tests).
5. **Final pass/fail table**: per-platform summary

Minimum validation checkpoints:
- `dotnet build` succeeds for both TFMs (browserwasm, desktop) validated across 3 desktop OSes
- `dotnet test` passes with appropriate filter per platform
- App launches and editor is interactive on each platform
- At least one property round-trip (Text set/get) verified per platform

### Agent-driven ad-hoc testing (optional, Windows)

If automated Playwright tests pass but manual testing reveals edge cases, the Playwright MCP pattern documented in Task 8 can be used for targeted investigation:
- Launch desktop app with CDP enabled
- Use Playwright MCP tools (`browser_snapshot`, `browser_evaluate`) for interactive debugging
- Not a formal test layer — a development/investigation tool

## Acceptance
- [ ] `dotnet test` passes on Windows (full suite including desktop CDP)
- [ ] `dotnet test MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj -- --filter-not-trait "Category=DesktopCDP" --filter-not-trait "Category=WasmPlaywright"` passes on macOS and Linux
<!-- Updated by plan-sync: fn-1-implement-desktop-skia-target-for.8 uses xUnit v3 MTP2 --filter-not-trait syntax, and added WasmPlaywright filter for local runs without Playwright -->
- [ ] Text editing works on Windows, macOS, and Linux
- [ ] Theme switching works on all three platforms
- [ ] Keyboard shortcuts work on all three platforms
- [ ] Decorations and markers render on all three platforms
- [ ] EditorLoading/EditorLoaded fire exactly once on all platforms
- [ ] All language services pass on Windows
- [ ] Language services verified on macOS/Linux (failures documented with platform/symptom)
- [ ] Multi-instance smoke-test passes on all 3 platforms
- [ ] WASM not regressed (automated Playwright + manual verification)
- [ ] Per-platform pass/fail matrix documented with evidence (commands, versions, results)
- [ ] Failure artifacts stored in `test-artifacts/` (unified with Task 8)
- [ ] Performance acceptable (10K+ line file smoke test)
- [ ] Bug fixes (if any) are root-cause-only — no refactoring or unrelated changes
