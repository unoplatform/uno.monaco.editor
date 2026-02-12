# fn-6-bug-fixes-disposal-resize-and-test.3 Complete Dispose implementation and WASM JS cleanup

## Description

**Size**: M — disposal chain across C# and JS

**Problem**: Multiple disposal/cleanup gaps:

1. **Incomplete `Dispose()`** (BUG 4): `CodeEditor.Dispose()` (~`CodeEditor.cs:418-427`) only disposes `_cssBroker` and `_parentAccessor`. Missing: presenter disposal, WebView event unsubscription, WebView disposal, TS-side `editor.dispose()`, `_initialized` reset.

2. **WASM EditorContext leak** (BUG 11): `EditorContext._editors` map in `otherScriptsToBeOrganized.ts:6` is never cleaned because WASM code path never calls `disposeEditor`. Each editor instance leaks its Monaco model and view.

3. **Test app tab close** (from test app): `TabView_TabCloseRequested` in `MainPage.xaml.cs:44-47` removes tab item but never calls `Dispose()` on the `EditorControl`, leaving everything alive.

**Files**:
- `MonacoEditorComponent/CodeEditor/CodeEditor.cs` — `Dispose()` method
- `MonacoEditorComponent/CodeEditor/CodeEditor.Events.cs` — event unsubscription
- `MonacoEditorComponent/CodeEditor/WasmCodeEditorPresenter.cs` — WASM-specific cleanup
- `MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs` — Desktop-specific cleanup
- `MonacoEditorComponent/ts-helpermethods/otherScriptsToBeOrganized.ts` — `disposeEditor()` function, `EditorContext._editors` cleanup
- `MonacoEditorTestApp/MainPage.xaml.cs` — `TabView_TabCloseRequested`

**Approach**:
1. Implement comprehensive `Dispose(bool disposing)` pattern:
   - Unsubscribe all C# events (`Loaded`, `Unloaded`, `SizeChanged`, etc.)
   - Invoke JS `disposeEditor()` to clean up Monaco instance
   - Dispose presenter, WebView, bridge handler
   - Set `_initialized = false`, `_disposed = true`
   - Guard public methods against use-after-dispose
2. Add/fix `disposeEditor()` in TS to remove from `EditorContext._editors` map and call `editor.dispose()`
3. Add WASM-specific cleanup: call `disposeEditor()` via JS interop in `WasmCodeEditorPresenter`
4. Fix test app: call `Dispose()` on `EditorControl` in `TabView_TabCloseRequested`

**Key context**:
- Monaco `editor.dispose()` must be called or the editor leaks DOM nodes and event listeners — see https://github.com/microsoft/monaco-editor/issues/4702
- The `Bridge/WebView2JsonRpcMessageHandler.cs` already has a proper `Dispose()` with `_disposed` guard — follow that pattern
- `getEditorForElement` auto-creates context on miss (BUG 12) — consider making it return null/throw if element not found, to surface bugs instead of masking them

## Acceptance
- [ ] `CodeEditor.Dispose()` cleans up all resources: events, presenter, WebView, JS-side Monaco instance
- [ ] WASM path calls `disposeEditor()` on cleanup
- [ ] `EditorContext._editors` map is cleaned when an editor is disposed
- [ ] Test app `TabView_TabCloseRequested` calls `Dispose()` on the editor
- [ ] `_disposed` guard prevents use-after-dispose
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm` succeeds

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
