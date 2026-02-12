# fn-6-bug-fixes-disposal-resize-and-test.2 Fix initialization race condition and event handler leaks

## Description

**Size**: M — lifecycle state machine refactor + event handler fixes

**Problem**: Two related bugs in the control's lifecycle:

1. **Dual initialization paths** (BUG 2): Both `WebView_NavigationCompleted` (~`CodeEditor.Events.cs:155`) and `CodeEditorLoaded` (~`CodeEditor.Events.cs:345`) set `_initialized = true` and call `ApplyInitialPropertyValues()`. If both fire, properties get applied twice.

2. **Event handler leaks** (BUG 3): `OnApplyTemplate` (~`CodeEditor.cs:299-301`) does `+=` on `NavigationCompleted` and `NewWindowRequested` without first doing `-=`. If `OnApplyTemplate` is called more than once (template re-application, theme changes), handlers stack up.

**Files**:
- `MonacoEditorComponent/CodeEditor/CodeEditor.cs` — `OnApplyTemplate`, constructor
- `MonacoEditorComponent/CodeEditor/CodeEditor.Events.cs` — `WebView_NavigationCompleted`, `CodeEditorLoaded`, `ApplyInitialPropertyValues()`, initialization logic
- `MonacoEditorComponent/CodeEditor/EditorLifecycleState.cs` — lifecycle state enum
- `MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs` — `BridgeHandshakeTarget.OnEditorReady`, wiring `editor/ready` to `TryCompleteInitialization()`
- `MonacoEditorComponent/CodeEditor/ICodeEditorPresenter.cs` — add readiness callback/event contract

**Approach**:
1. Add a readiness callback or event to `ICodeEditorPresenter` so each presenter can signal when its editor is actually ready.
2. Define readiness per presenter path:
   - Desktop: `editor/ready` signal (currently only logged in `DesktopCodeEditorPresenter.BridgeHandshakeTarget.OnEditorReady`) must be wired to call `TryCompleteInitialization()` on `CodeEditor`. `bridge/ready` is transport-only prerequisite.
   - WASM: managed `Loaded` callback path signals readiness.
3. Implement `TryCompleteInitialization()` on `CodeEditor` with a one-shot transition guard keyed to `EditorLifecycleState`: only the first valid transition to `Loaded` can invoke `ApplyInitialPropertyValues()`. Guard must be set *before* calling init logic.
4. Guard `OnApplyTemplate` event subscriptions: `-=` before `+=` for `NavigationCompleted`, `NewWindowRequested`, and any other WebView event handlers.
5. Ensure `Unloaded` event properly resets lifecycle state for control re-use scenarios.

**Key context**:
- `bridge/ready` fires at bundle load (`ts-helpermethods/index.ts`) BEFORE Monaco editor creation — it only means the JS transport is available
- `editor/ready` fires after `editor.create()` completes — this is the correct Desktop readiness signal
- Currently `OnEditorReady` in `DesktopCodeEditorPresenter` only logs; it needs to propagate up to `CodeEditor`
- `ApplyInitialPropertyValues()` lives in `CodeEditor.Events.cs` (not Properties.cs)
- Uno Platform controls can have `OnApplyTemplate` called multiple times

## Acceptance
- [ ] `ICodeEditorPresenter` has a readiness callback/event that presenters use to signal editor-ready
- [ ] Desktop: `editor/ready` → `OnEditorReady` → presenter callback → `TryCompleteInitialization()` on `CodeEditor`
- [ ] WASM: managed Loaded callback → `TryCompleteInitialization()` on `CodeEditor`
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
