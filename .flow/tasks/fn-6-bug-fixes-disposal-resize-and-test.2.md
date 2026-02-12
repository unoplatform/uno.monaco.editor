# fn-6-bug-fixes-disposal-resize-and-test.2 Fix initialization race condition and event handler leaks

## Description

**Size**: M — lifecycle state machine refactor + event handler fixes

**Problem**: Two related bugs in the control's lifecycle:

1. **Dual initialization paths** (BUG 2): Both `WebView_NavigationCompleted` (~`CodeEditor.Events.cs:155`) and `CodeEditorLoaded` (~`CodeEditor.Events.cs:345`) set `_initialized = true` and call `ApplyInitialPropertyValues()`. If both fire, properties get applied twice.

2. **Event handler leaks** (BUG 3): `OnApplyTemplate` (~`CodeEditor.cs:299-301`) does `+=` on `NavigationCompleted` and `NewWindowRequested` without first doing `-=`. If `OnApplyTemplate` is called more than once (template re-application, theme changes), handlers stack up.

**Files**:
- `MonacoEditorComponent/CodeEditor/CodeEditor.cs` — `OnApplyTemplate`, constructor
- `MonacoEditorComponent/CodeEditor/CodeEditor.Events.cs` — `WebView_NavigationCompleted`, `CodeEditorLoaded`, initialization logic
- `MonacoEditorComponent/CodeEditor/CodeEditor.Properties.cs` — `ApplyInitialPropertyValues()`
- `MonacoEditorComponent/CodeEditor/EditorLifecycleState.cs` — lifecycle state enum

**Approach**:
1. Define readiness per presenter: Desktop keys off bridge/editor-ready signal; WASM keys off the managed `Loaded` callback path. Unify through a single `TryCompleteInitialization()` gate on `CodeEditor`.
2. Use a one-shot transition guard keyed to `EditorLifecycleState`: only the first valid transition to `Loaded` can invoke `ApplyInitialPropertyValues()`. The guard must be set *before* calling init logic so re-entrant calls are blocked.
3. Guard `OnApplyTemplate` event subscriptions: `-=` before `+=` for `NavigationCompleted`, `NewWindowRequested`, and any other WebView event handlers.
4. Ensure `Unloaded` event properly resets lifecycle state for control re-use scenarios.

**Key context**:
- `NavigationCompleted` is the readiness signal on Desktop (WebView2), but is NOT the reliable signal on WASM (no WebView). Each presenter must define its own readiness contract.
- Pattern reference: `.flow/memory/pitfalls.md` entries on lifecycle and event handler leaks
- Uno Platform controls can have `OnApplyTemplate` called multiple times (theme changes, visual tree rebuilds)

## Acceptance
- [ ] Per-presenter readiness gates — Desktop via bridge-ready, WASM via managed Loaded path
- [ ] One-shot `TryCompleteInitialization()` — `ApplyInitialPropertyValues()` called exactly once per lifecycle
- [ ] `OnApplyTemplate` uses `-=` before `+=` for all event subscriptions
- [ ] Re-applying template does not cause double event handler subscriptions
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds
- [ ] Existing tests continue to pass

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
