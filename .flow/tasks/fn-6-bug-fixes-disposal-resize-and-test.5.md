# fn-6-bug-fixes-disposal-resize-and-test.5 Add unit tests for lifecycle disposal and data correctness

## Description

**Size**: L — new test authoring across multiple bug fix areas

**Problem**: The bug fixes in tasks 2-4 need regression tests to prevent reintroduction. Current test coverage is low for:
- Control lifecycle (init, dispose, re-template)
- Event handler subscription/unsubscription
- Property value propagation with correct types
- Error handling paths

**Files**:
- `MonacoEditorComponent.Tests/` — existing test project (xUnit v3)
- New test files for each bug fix area

**Approach**:

### Pure unit tests (always run, no platform dependencies):
1. **Property tests**:
   - Test `DecorationsProperty` accepts `IObservableVector<IModelDeltaDecoration>` values
   - Test `MarkersProperty` accepts correct collection type
   - Test property callback error handling doesn't swallow exceptions

2. **Data correctness tests** (validated through C#-observable paths):
   - Test `BridgeEncoding` round-trips with `\n`, `\r\n`, `\r`, `%`, and `'` characters
   - Test `RunScriptHelperAsync` with null return value (via mock WebView)
   - Test navigation allowlist logic

3. **Disposal state tests**:
   - Test `_disposed` flag is set after `Dispose()`
   - Test double-dispose is safe (no-throw)
   - Test use-after-dispose guards on public methods

### Integration-style tests (may require platform fixtures):
4. **Lifecycle tests** (use `MockCodeEditorPresenter` to avoid real WebView):
   - Test that `TryCompleteInitialization` only completes when presenter signals readiness
   - Test that `Dispose()` unsubscribes events
   - If full control tree is needed and can't be mocked, mark with `[Skip("Requires platform UI thread")]` with documented reason

5. **Tab close disposal** (if feasible with mock presenter):
   - Test editor creation and disposal cycle via `MockCodeEditorPresenter`

**Skip policy**: Tests requiring a live UI thread, WebView2, or browser runtime should be marked with `[Skip("reason")]` and documented reason. These are candidates for Playwright integration tests (separate epic).

**Key context**:
- xUnit v3 is the test framework; use `Microsoft.Testing.Extensions.CodeCoverage` (not coverlet) for coverage
- See MEMORY.md for xUnit v3 fixture patterns (collection fixtures cannot inject each other)
- `MockCodeEditorPresenter` already exists in the test project — extend it as needed
- No JavaScript test framework (Vitest/Jest) in scope — all TS behavior validated through C#-observable paths

## Acceptance
- [ ] Pure unit tests for property type correctness
- [ ] Pure unit tests for BridgeEncoding round-trip with edge-case characters
- [ ] Pure unit tests for null-safety in RunScriptHelperAsync
- [ ] Pure unit tests for disposal state management (_disposed flag, double-dispose)
- [ ] Integration-style tests for lifecycle initialization gate (via MockCodeEditorPresenter)
- [ ] Integration-style tests for event handler leak prevention (via MockCodeEditorPresenter)
- [ ] Any skipped tests have documented `[Skip("reason")]` with clear justification
- [ ] All non-skipped tests pass: `dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj`

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
