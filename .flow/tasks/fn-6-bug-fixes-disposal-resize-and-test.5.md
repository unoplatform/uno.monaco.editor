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
1. **Lifecycle tests**:
   - Test that `TryCompleteInitialization` only completes when presenter signals readiness
   - Test that `OnApplyTemplate` called twice doesn't double-subscribe events
   - Test that `Dispose()` sets `_disposed = true` and unsubscribes events
   - Test that methods throw/no-op after dispose

2. **Property tests**:
   - Test `DecorationsProperty` accepts `IObservableVector<IModelDeltaDecoration>` values
   - Test `MarkersProperty` accepts correct collection type
   - Test `SelectedTextProperty` change callback error handling

3. **Data correctness tests** (validated through C#-observable integration paths, not JS-level tests):
   - Test that `BridgeEncoding` round-trips strings with `\n`, `\r\n`, `\r`, `%`, and `'` correctly (exercises sanitize/desanitize indirectly)
   - Test `RunScriptHelperAsync` with null return value (via mock WebView)
   - Test EOL-sensitive code paths through the C# API surface

4. **Integration-like tests** (if feasible without live WebView):
   - Test editor creation and disposal cycle via `MockCodeEditorPresenter`
   - Test tab close triggers proper cleanup

**Key context**:
- xUnit v3 is the test framework; use `Microsoft.Testing.Extensions.CodeCoverage` (not coverlet) for coverage
- See MEMORY.md for xUnit v3 fixture patterns (collection fixtures cannot inject each other)
- Tests needing WebView2 should be marked with appropriate skip conditions for CI
- No JavaScript test framework (Vitest/Jest) in scope — all TS behavior is validated through C#-observable integration paths

## Acceptance
- [ ] Unit tests exist for per-presenter initialization gates (one-shot, idempotent)
- [ ] Unit tests exist for event handler leak prevention
- [ ] Unit tests exist for `Dispose()` completeness
- [ ] Unit tests exist for property type correctness
- [ ] Unit tests exist for BridgeEncoding round-trip with edge-case characters
- [ ] Unit tests exist for null-safety in `RunScriptHelperAsync`
- [ ] All tests pass: `dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj`
- [ ] No skipped tests without documented reason

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
