# Bug Fixes, Disposal, Resize, and Test Coverage

## Overview

Comprehensive bug-fixing epic for the `uno.monaco.editor` control and test app. Addresses confirmed bugs found through deep codebase analysis, covering resize/layout issues, initialization race conditions, incomplete disposal, data correctness problems, and error handling gaps. Also expands unit test coverage to 85%+ on both WASM and Desktop platforms.

## Bug Inventory

| # | Bug | Severity | Task | Status |
|---|-----|----------|------|--------|
| 1 | WASM LayoutUpdated fires every frame (resize perf) | HIGH | .1 | Open |
| 2 | Dual initialization paths (race condition) | HIGH | .2 | Open |
| 3 | OnApplyTemplate leaks event handlers | MEDIUM | .2 | Open |
| 4 | Dispose() incomplete (C# side) | MEDIUM | .3 | Open |
| 5 | Desktop AllowedFileContentRoot never set | MEDIUM | — | Deferred (requires fn-1 content delivery) |
| 6 | NullReferenceException in RunScriptHelperAsync | MEDIUM | .4 | Open |
| 7 | updateSelectedContent.ts uses \r instead of \n | MEDIUM | .4 | Open |
| 8 | DecorationsProperty/MarkersProperty wrong type registration | MEDIUM | .4 | Open |
| 9 | async void swallows exceptions in property callbacks | LOW | .4 | Open |
| 10 | SelectedTextProperty fire-and-forget | LOW | .4 | Open |
| 11 | WASM EditorContext._editors map never cleaned | LOW | .3 | Open |
| 12 | getEditorForElement auto-creates on miss | LOW | .3 | Open |
| 13 | sanitize/desanitize ordering (verify if still present) | LOW | .4 | Open — verify first |
| 14 | Test app tab close doesn't call Dispose | LOW | .3 | Open |

## Scope

### In Scope
- Merge PR #37 (WASM ResizeObserver fix)
- Fix initialization race condition (per-presenter readiness gates)
- Complete `Dispose()` implementation (C# + JS-side cleanup)
- Fix event handler leaks in `OnApplyTemplate`
- Fix `DecorationsProperty`/`MarkersProperty` type registration
- Fix `updateSelectedContent.ts` EOL handling
- Fix `RunScriptHelperAsync` null reference
- Fix async void exception swallowing in property callbacks
- Fix WASM EditorContext leak (no `disposeEditor` called)
- Fix test app tab close disposal
- Add unit tests for lifecycle, disposal, and data correctness
- Configure code coverage reporting (85%+ line coverage target)
- Update changelog.md with bug fix entries

### Out of Scope
- Desktop content delivery completion (folder mapping / fn-1 Task 3) — this includes BUG 5 (AllowedFileContentRoot), which requires the content delivery infrastructure to be meaningful
- New feature additions
- Monaco editor version upgrade
- Formatting or style-only changes
- JavaScript-level test framework (Vitest/Jest) — TS behavior validated through C#-observable integration paths

## Approach

Work from foundational fixes upward, serialized to minimize merge conflicts:
1. **Resize fix** (PR #37 merge) — standalone, no deps
2. **Lifecycle/init fixes** — per-presenter readiness gates, event handler leak prevention
3. **Disposal/cleanup** — complete `Dispose()`, add WASM JS cleanup, fix tab close
4. **Data correctness** — property types, EOL, null safety, error handling (serialized after .2 to avoid file overlap)
5. **Test coverage** — unit tests for all fixes, validate TS behaviors through C#-observable paths
6. **Coverage + changelog** — systematic expansion to 85%+ line coverage, changelog update, bug ledger closure

## Quick commands

```bash
# Build and verify
dotnet build MonacoEditorComponent.slnx --no-restore

# Run unit tests
dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj

# Run with coverage
dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj -- --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
```

## Risks

- PR #37 may have merge conflicts with current branch (mitigate: review diff before cherry-pick)
- 85% line coverage target may be hard for code paths requiring live WebView2/WASM runtime (mitigate: mock-based unit tests, `[ExcludeFromCodeCoverage]` with documented rationale for untestable interop paths)
- Disposal changes may affect test app behavior (mitigate: verify on both WASM and Desktop targets)
- BUG 13 (sanitize ordering) may already be resolved — verify before fixing

## Acceptance

- [ ] All bugs in inventory have fixes or documented deferral rationale
- [ ] PR #37 resize fix is merged and working
- [ ] `Dispose()` comprehensively cleans up all resources (C# events, TS Monaco instance, DOM)
- [ ] No initialization race conditions (per-presenter readiness gate, one-shot transition)
- [ ] Line coverage >= 85% on `MonacoEditorComponent` assembly (net10.0-desktop TFM, `[ExcludeFromCodeCoverage]` exclusions documented)
- [ ] `dotnet build MonacoEditorComponent.slnx` succeeds
- [ ] `dotnet test` passes (unit tests + available integration tests)
- [ ] changelog.md updated with bug fix entries and deferral notes

## References

- PR #37: https://github.com/unoplatform/uno.monaco.editor/pull/37
- PR #38: https://github.com/unoplatform/uno.monaco.editor/pull/38
- Monaco ResizeObserver: https://github.com/microsoft/monaco-editor/issues/3051
- Monaco dispose issues: https://github.com/microsoft/monaco-editor/issues/4702
- Uno WASM resize lag: https://github.com/unoplatform/uno/issues/22144
- Monaco multi-instance themes: https://github.com/microsoft/monaco-editor/issues/4425
- Memory pitfalls: `.flow/memory/pitfalls.md`
