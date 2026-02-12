# Bug Fixes, Disposal, Resize, and Test Coverage

## Overview

Comprehensive bug-fixing epic for the `uno.monaco.editor` control and test app. Addresses confirmed bugs found through deep codebase analysis (7 parallel analysis agents covering lifecycle, disposal, data correctness, desktop WebView2, WASM interop, test app, and Uno Platform issues). Covers resize/layout issues, initialization race conditions, incomplete disposal, data correctness problems, threading violations, and error handling gaps. Also expands unit test coverage to 85%+ line coverage.

## Bug Inventory

| # | Bug | Severity | Task | Status |
|---|-----|----------|------|--------|
| 1 | WASM LayoutUpdated fires every frame (resize perf) | HIGH | .1 | Open |
| 2 | Dual initialization paths (race condition); `_initialized` and `_lifecycleState` diverge | HIGH | .2 | Open |
| 3 | OnApplyTemplate leaks event handlers; CoreWebView2 handlers never detached on teardown | MEDIUM | .2 | Open |
| 4 | Dispose() incomplete (C# side); uses `new` keyword hiding base IDisposable | MEDIUM | .3 | Open |
| 5 | Desktop AllowedFileContentRoot never set | MEDIUM | — | Deferred (requires fn-1 content delivery) |
| 6 | NullReferenceException in RunScriptHelperAsync | MEDIUM | .4 | Open |
| 7 | updateSelectedContent.ts uses `\r` instead of `\n` | MEDIUM | .4 | Open |
| 8 | DecorationsProperty/MarkersProperty wrong type registration | MEDIUM | .4 | Open |
| 9 | async void swallows exceptions in property callbacks | LOW | .4 | Open |
| 10 | SelectedTextProperty fire-and-forget | LOW | .4 | Open |
| 11 | WASM EditorContext._editors map never cleaned; model not disposed | HIGH | .3 | Open |
| 12 | getEditorForElement auto-creates on miss; registerEditorForElement overwrites silently | LOW | .3 | Open |
| 13 | sanitize/desanitize ordering | LOW | — | Already verified — covered by BridgeEncodingTests |
| 14 | Test app tab close doesn't call Dispose; EditorControl not IDisposable | LOW | .3 | Open |
| 15 | GetJsonValue reads DependencyProperties off UI thread (desktop crash) | CRITICAL | .4 | Open |
| 16 | KeyboardListenerDesktop.OnKeyDown fires events off UI thread | HIGH | .4 | Open |
| 17 | Options_PropertyChanged reverts GlyphMargin/ReadOnly instead of propagating | MEDIUM | .2 | Open |
| 18 | Monaco event disposables (onDidChangeContent, onDidChangeCursorSelection) discarded | HIGH | .3 | Open |
| 19 | ManagedCallEvent double-desanitize on return value | MEDIUM | .4 | Open |
| 20 | ManagedSetValue destructive string processing (trim quotes, backslash collapse) | MEDIUM | .4 | Open |
| 21 | getThemeIsHighContrast compares boolean to string "true" (always false) | MEDIUM | .4 | Open |
| 22 | document.body.style.overflow = 'hidden' global side effect, never restored | MEDIUM | .3 | Open |
| 23 | getSrc/setSrc JSImport reference non-existent globals | MEDIUM | .4 | Open |
| 24 | DropOldest channel policy silently drops JSON-RPC messages | MEDIUM | .4 | Open |
| 25 | PostWebMessage potentially called off UI thread | MEDIUM | .4 | Open |
| 26 | BrowserHtmlElement DOM node never removed from visual tree | MEDIUM | .3 | Open |
| 27 | IThemeListener missing IDisposable contract | MEDIUM | .3 | Open |
| 28 | Test app EditorControl Remove path missing Unloaded/PropertyChanged unsubscribe | MEDIUM | .3 | Open |
| 29 | Test app NullRef in all button handlers after Remove | MEDIUM | .3 | Open |
| 30 | Test app TextBox OneTime binding never shows loaded content | MEDIUM | .3 | Open |
| 31 | InitialiseWebObjects creates JsonRpc before Launch() wires CoreWebView2 (race) | HIGH | .2 | Open |

## Scope

### In Scope
- Merge PR #37 (WASM ResizeObserver fix)
- Fix initialization race condition (per-presenter readiness gates)
- Complete `Dispose()` implementation (C# + JS-side cleanup)
- Fix event handler leaks in `OnApplyTemplate` and CoreWebView2
- Fix `DecorationsProperty`/`MarkersProperty` type registration
- Fix `updateSelectedContent.ts` EOL handling
- Fix `RunScriptHelperAsync` null reference
- Fix async void exception swallowing in property callbacks
- Fix WASM EditorContext leak (no `disposeEditor` called)
- Fix test app tab close disposal and EditorControl bugs
- Fix desktop threading violations (GetJsonValue, KeyboardListener, PostWebMessage)
- Fix data corruption (double-desanitize, ManagedSetValue, getThemeIsHighContrast)
- Fix Options_PropertyChanged GlyphMargin/ReadOnly direction
- Fix Monaco event disposable leaks
- Add unit tests for **every bug** — tests must demonstrate the fix works and prevent regression
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
2. **Lifecycle/init fixes** — per-presenter readiness gates (Desktop: `editor/ready`, WASM: managed Loaded), event handler leak prevention, Options_PropertyChanged direction fix, InitialiseWebObjects ordering
3. **Disposal/cleanup** — add `IDisposable` to presenter contract, complete `Dispose()`, add WASM JS cleanup, fix tab close, fix Monaco event disposable leaks, fix test app EditorControl bugs
4. **Data correctness** — property types, EOL, null safety, error handling, threading violations, data corruption fixes (serialized after .2 to avoid file overlap)
5. **Test coverage** — unit tests for all 31 bugs, validate TS behaviors through C#-observable paths
6. **Coverage + changelog** — systematic expansion to 85%+ line coverage, changelog update, bug ledger closure

## Quick commands

```bash
# Build and verify
dotnet build MonacoEditorComponent.slnx --no-restore

# Run unit tests
dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj

# Run with coverage (tests target net10.0)
dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj -- --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
```

## Risks

- PR #37 may have merge conflicts with current branch (mitigate: review diff before cherry-pick)
- 85% line coverage target may be hard for code paths requiring live WebView2/WASM runtime (mitigate: mock-based unit tests, `[ExcludeFromCodeCoverage]` with documented rationale for untestable interop paths)
- Disposal changes may affect test app behavior (mitigate: verify on both WASM and Desktop targets)
- Desktop threading fixes may require architectural changes to JSON-RPC dispatch (mitigate: UI thread marshaling via DispatcherQueue)

## Acceptance

- [ ] All bugs in inventory have fixes or documented deferral/verification rationale
- [ ] PR #37 resize fix is merged and working
- [ ] `Dispose()` comprehensively cleans up all resources (C# events, TS Monaco instance, DOM)
- [ ] No initialization race conditions (per-presenter readiness gate, one-shot transition)
- [ ] Every bug has at least one regression test demonstrating the fix
- [ ] Line coverage >= 85% on `MonacoEditorComponent` assembly (tests run on `net10.0` TFM, `[ExcludeFromCodeCoverage]` exclusions documented)
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
- Uno WebView2 Source issue: https://github.com/unoplatform/uno/issues/19769
- Memory pitfalls: `.flow/memory/pitfalls.md`
