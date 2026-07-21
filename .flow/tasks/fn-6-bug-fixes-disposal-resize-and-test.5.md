# fn-6-bug-fixes-disposal-resize-and-test.5 Add unit tests for all 31 bugs — lifecycle, disposal, data correctness, and threading

## Description

**Size**: L — comprehensive test authoring across all bug fix areas

**Problem**: Every bug fix in tasks .1-.4 needs regression tests to prevent reintroduction AND demonstrate the fix works. The deep analysis identified 31 bugs total, each requiring at least one test. Current test coverage is low for:
- Control lifecycle (init, dispose, re-template)
- Event handler subscription/unsubscription
- Property value propagation with correct types
- Error handling paths
- Threading violations (UI thread marshaling)
- Data corruption paths (desanitize, string processing)

**Files**:
- `MonacoEditorComponent.Tests/` — existing test project (xUnit v3)
- New test files organized by bug area

**Approach**:

### Per-Bug Test Matrix

Every bug MUST have at least one test. Tests marked `[Skip]` must have documented justification.

#### Task .1 (Resize) Tests:
<!-- Updated by plan-sync: fn-6.1 already delivered 4 regression tests in WasmResizeRegressionTests (NoLayoutUpdatedSubscription, NoRefreshLayoutPInvoke, NoRefreshLayoutTsExport, ResizeObserverInDisposeEditor). These are DONE and should not be re-created. -->
| Bug | Test | Type | Status |
|-----|------|------|--------|
| BUG 1 | Verify WasmCodeEditorPresenter no longer uses LayoutUpdated | Unit (reflection) | DONE in fn-6.1 |
| BUG 1 | Verify NativeMethods.RefreshLayout P/Invoke removed | Unit (reflection) | DONE in fn-6.1 |
| BUG 1 | Verify refreshLayout not exported to globalThis | Unit (reflection) | DONE in fn-6.1 |
| BUG 1 | Verify disposeEditor disconnects ResizeObserver (not window resize listener) | Unit (reflection) | DONE in fn-6.1 |
| BUG 1 | Verify ResizeObserver is set up in TS (via C#-observable path) | Integration (skip if no browser) | Pending |

#### Task .2 (Lifecycle) Tests:
| Bug | Test | Type |
|-----|------|------|
| BUG 2 | `ApplyInitialPropertyValues` called exactly once per lifecycle | Unit (MockPresenter) |
| BUG 2 | `_initialized` and lifecycle state consistent after init | Unit (MockPresenter) |
| BUG 2 | Rapid load/unload/load cycle → no double init | Unit (MockPresenter) |
| BUG 3 | `OnApplyTemplate` x2 → no double event handler subscriptions | Unit (MockPresenter) |
| BUG 3 | CoreWebView2 handlers detached on normal teardown | Unit (mock) |
| BUG 17 | `Options.GlyphMargin` change propagates to `HasGlyphMargin` | Unit |
| BUG 17 | `Options.ReadOnly` change propagates to `ReadOnly` | Unit |
| BUG 17 | `updateOptions` called exactly once per property change | Unit (mock interop) |
| BUG 31 | JSON-RPC does not start before transport is wired | Unit (MockPresenter) |

#### Task .3 (Disposal) Tests:
| Bug | Test | Type |
|-----|------|------|
| BUG 4 | `Dispose()` sets `_disposed=true`, `_initialized=false` | Unit |
| BUG 4 | Double-dispose is safe (no throw) | Unit |
| BUG 4 | `Dispose()` via `IDisposable` reference calls correct implementation | Unit |
| BUG 4 | Event handlers unsubscribed after Dispose | Unit |
| BUG 11 | EditorContext._editors map cleaned after dispose | Integration (skip if no browser) |
| BUG 11 | Model disposed in disposeEditor | Integration (skip if no browser) |
| BUG 18 | Monaco event disposables stored and disposed | Integration (skip if no browser) |
| BUG 12 | `getEditorForElement` throws on miss | Integration (skip if no browser) |
| BUG 12 | `tryGetEditorForElement` returns null on miss | Integration (skip if no browser) |
| BUG 12 | `registerEditorForElement` disposes old editor | Integration (skip if no browser) |
| BUG 14 | Test app `TabView_TabCloseRequested` calls Dispose | Unit (mock) |
| BUG 22 | `document.body.style.overflow` restored on last editor dispose | Integration (skip if no browser) |
| BUG 26 | `BrowserHtmlElement` removed from DOM on dispose | Integration (skip if no browser) |
| BUG 27 | `IThemeListener` implementations disposable | Unit |
| BUG 28 | EditorControl Remove path unsubscribes events | Unit (mock) |
| BUG 29 | Button handlers guard null CodeEditor (no throw) | Unit |
| BUG 30 | TextBox binding updates on content load | Unit (mock) |
| All | Use-after-dispose on public methods → `ObjectDisposedException` | Unit |

#### Task .4 (Data Correctness) Tests:
| Bug | Test | Type |
|-----|------|------|
| BUG 8 | `DecorationsProperty` accepts `IObservableVector<IModelDeltaDecoration>` | Unit |
| BUG 8 | `MarkersProperty` accepts correct collection type | Unit |
| BUG 7 | `updateSelectedContent` end line/column correct with `\n` | Integration (skip if no browser) |
| BUG 7 | `updateSelectedContent` end line/column correct with `\r\n` | Integration (skip if no browser) |
| BUG 6 | `RunScriptHelperAsync` null return → no NRE | Unit (mock WebView) |
| BUG 9 | Property callbacks propagate exceptions | Unit |
| BUG 10 | `SelectedTextProperty` callback surfaces errors | Unit |
| BUG 15 | `GetJsonValue` executes on UI thread | Unit (mock DispatcherQueue) |
| BUG 16 | `OnKeyDown` marshals to UI thread | Unit (mock DispatcherQueue) |
| BUG 19 | `ManagedCallEvent` return desanitized exactly once | Unit (round-trip) |
| BUG 20 | `ManagedSetValue` preserves quotes and backslashes | Unit (round-trip) |
| BUG 21 | `getThemeIsHighContrast` returns true in HC mode | Integration (skip if no browser) |
| BUG 23 | `getSrc`/`setSrc` removed or functional | Unit (reflection) |
| BUG 24 | Bounded channel doesn't silently drop messages | Unit (stress) |
| BUG 25 | `PostWebMessage` called on UI thread | Unit (mock) |
| BUG 13 | BridgeEncoding round-trip with `%`, `\`, `"`, `'`, `\r\n` | Unit (existing + extend) |

### Test Organization

New test files:
- `LifecycleTests.cs` — BUGs 2, 3, 17, 31
- `DisposalTests.cs` — BUGs 4, 11, 12, 14, 18, 22, 26, 27
- `DataCorrectnessTests.cs` — BUGs 6, 7, 8, 9, 10
- `ThreadingTests.cs` — BUGs 15, 16, 25
- `InteropDataTests.cs` — BUGs 19, 20, 21, 23
- `BridgeChannelTests.cs` — BUG 24
- `TestAppTests.cs` — BUGs 28, 29, 30

### Skip Policy
Tests requiring a live UI thread, WebView2, or browser runtime should be marked with `[Skip("reason")]` and documented reason. These are candidates for Playwright integration tests (separate epic).

**Key context**:
- xUnit v3 is the test framework; use `Microsoft.Testing.Extensions.CodeCoverage` (not coverlet) for coverage
- See MEMORY.md for xUnit v3 fixture patterns (collection fixtures cannot inject each other)
- `MockCodeEditorPresenter` already exists in the test project — extend it as needed
- No JavaScript test framework (Vitest/Jest) in scope — all TS behavior validated through C#-observable paths

## Acceptance
- [ ] Every bug (1-31) has at least one test
- [ ] Pure unit tests for: property types (8), null safety (6), disposal state (4), threading marshaling (15, 16, 25)
- [ ] Pure unit tests for: data integrity (19, 20), exception propagation (9, 10)
- [ ] Integration-style tests for lifecycle init gate (2, 3, 17, 31) via MockCodeEditorPresenter
- [ ] Integration-style tests for event handler leak prevention (3) via MockCodeEditorPresenter
- [ ] Skipped tests for browser-only paths (7, 11, 12, 18, 21, 22, 26) with `[Skip("reason")]`
- [ ] All non-skipped tests pass: `dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj`
- [ ] Test coverage meaningfully improved (target: 85%+ in task .6)

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
