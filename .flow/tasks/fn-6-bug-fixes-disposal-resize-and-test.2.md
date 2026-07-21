# fn-6-bug-fixes-disposal-resize-and-test.2 Fix initialization race condition and event handler leaks

## Description

**Size**: M — lifecycle state machine refactor + event handler fixes

**Problem**: Multiple related bugs in the control's lifecycle:

1. **Dual initialization paths** (BUG 2): Both `WebView_NavigationCompleted` (~`CodeEditor.Events.cs:155`) and `CodeEditorLoaded` (~`CodeEditor.Events.cs:345`) set `_initialized = true` and call `ApplyInitialPropertyValues()`. If both fire, properties get applied twice. Root cause: `_initialized` boolean and `_lifecycleState` enum track overlapping concerns independently, creating windows where they disagree.

2. **Event handler leaks** (BUG 3): `OnApplyTemplate` (~`CodeEditor.cs:299-301`) does `+=` on `NavigationCompleted` and `NewWindowRequested` without first doing `-=`. If `OnApplyTemplate` is called more than once, handlers stack up. Additionally, CoreWebView2 event handlers (`DetachCoreWebView2Handlers`) are only called in the `catch` block of `Launch()`, never on normal teardown.

3. **Options_PropertyChanged direction bug** (BUG 17): `Options_PropertyChanged` in `CodeEditor.cs:138-161` reverts `GlyphMargin` and `ReadOnly` changes instead of propagating them. When `Options.GlyphMargin` changes, the handler sets `options.GlyphMargin = HasGlyphMargin` (reverting) instead of `HasGlyphMargin = options.GlyphMargin` (propagating). Same for `ReadOnly`. Also calls `updateOptions` unconditionally after every property change (double `updateOptions` call).

4. **InitialiseWebObjects ordering** (BUG 31): `WebView_DOMContentLoaded` calls `InitialiseWebObjects()` (which creates JsonRpc + starts listening) BEFORE calling `Launch()` (which calls `EnsureCoreWebView2Async` and wires `WebMessageReceived`). The JSON-RPC starts listening on a channel with no transport. `bridge/ready` from JS may be lost if it fires before `WebMessageReceived` is wired.

**Files**:
- `MonacoEditorComponent/CodeEditor/CodeEditor.cs` — `OnApplyTemplate`, constructor, `Options_PropertyChanged`
- `MonacoEditorComponent/CodeEditor/CodeEditor.Events.cs` — `WebView_NavigationCompleted`, `CodeEditorLoaded`, `ApplyInitialPropertyValues()`, `WebView_DOMContentLoaded`, initialization logic
- `MonacoEditorComponent/CodeEditor/EditorLifecycleState.cs` — lifecycle state enum
- `MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs` — `BridgeHandshakeTarget.OnEditorReady`, wiring `editor/ready` to `TryCompleteInitialization()`, `DetachCoreWebView2Handlers`
- `MonacoEditorComponent/CodeEditor/ICodeEditorPresenter.cs` — add readiness callback/event contract

**Approach**:
1. Add a readiness callback or event to `ICodeEditorPresenter` so each presenter can signal when its editor is actually ready.
2. Define readiness per presenter path:
   - Desktop: `editor/ready` signal (currently only logged in `DesktopCodeEditorPresenter.BridgeHandshakeTarget.OnEditorReady`) must be wired to call `TryCompleteInitialization()` on `CodeEditor`. `bridge/ready` is transport-only prerequisite.
   - WASM: managed `Loaded` callback path signals readiness.
3. Implement `TryCompleteInitialization()` on `CodeEditor` with a one-shot transition guard keyed to `EditorLifecycleState`: only the first valid transition to `Loaded` can invoke `ApplyInitialPropertyValues()`. Guard must be set *before* calling init logic.
4. Guard `OnApplyTemplate` event subscriptions: `-=` before `+=` for `NavigationCompleted`, `NewWindowRequested`, and any other WebView event handlers.
5. Fix `Options_PropertyChanged` direction: propagate changes FROM `Options` TO pass-through properties (not reverse). Avoid double `updateOptions`.
6. Fix `WebView_DOMContentLoaded` ordering: call `Launch()` before `InitialiseWebObjects()`, or defer JSON-RPC startup until transport is confirmed.
7. Ensure `DetachCoreWebView2Handlers` is called on normal teardown (not just error path).
8. Ensure `Unloaded` event properly resets lifecycle state for control re-use scenarios.

**Key context**:
- `bridge/ready` fires at bundle load (`ts-helpermethods/index.ts`) BEFORE Monaco editor creation — it only means the JS transport is available
- `editor/ready` fires after `editor.create()` completes — this is the correct Desktop readiness signal
- Currently `OnEditorReady` in `DesktopCodeEditorPresenter` only logs; it needs to propagate up to `CodeEditor`
- `ApplyInitialPropertyValues()` lives in `CodeEditor.Events.cs` (not Properties.cs)
- Uno Platform controls can have `OnApplyTemplate` called multiple times

## Required Tests

Each bug fix MUST have corresponding test(s):

- **BUG 2 test**: Verify `ApplyInitialPropertyValues()` is called exactly once per lifecycle (mock presenter fires readiness twice → second is no-op)
- **BUG 2 test**: Verify `_initialized` and lifecycle state are consistent after initialization
- **BUG 3 test**: Verify `OnApplyTemplate` called twice does NOT double-subscribe event handlers (count handler invocations)
- **BUG 3 test**: Verify CoreWebView2 handlers are detached on normal teardown
- **BUG 17 test**: Verify changing `Options.GlyphMargin` propagates to `HasGlyphMargin` (not reverted)
- **BUG 17 test**: Verify changing `Options.ReadOnly` propagates to `ReadOnly` property (not reverted)
- **BUG 17 test**: Verify `updateOptions` is called exactly once per property change
- **BUG 31 test**: Verify JSON-RPC startup does not occur before transport is wired (via mock presenter)

## Acceptance
- [ ] `ICodeEditorPresenter` has a readiness callback/event that presenters use to signal editor-ready
- [ ] Desktop: `editor/ready` → `OnEditorReady` → presenter callback → `TryCompleteInitialization()` on `CodeEditor`
- [ ] WASM: managed Loaded callback → `TryCompleteInitialization()` on `CodeEditor`
- [ ] One-shot `TryCompleteInitialization()` — `ApplyInitialPropertyValues()` called exactly once per lifecycle
- [ ] `OnApplyTemplate` uses `-=` before `+=` for all event subscriptions
- [ ] Re-applying template does not cause double event handler subscriptions
- [ ] `Options_PropertyChanged` propagates changes correctly (GlyphMargin, ReadOnly)
- [ ] `DetachCoreWebView2Handlers` called on normal teardown
- [ ] `WebView_DOMContentLoaded` ordering ensures transport before JSON-RPC startup
- [ ] Each bug has at least one regression test
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds
- [ ] Existing tests continue to pass

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
