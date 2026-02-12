# fn-6-bug-fixes-disposal-resize-and-test.3 Complete Dispose implementation and WASM JS cleanup

## Description

**Size**: M — disposal chain across C# and JS

**Problem**: Multiple disposal/cleanup gaps:

1. **Incomplete `Dispose()`** (BUG 4): `CodeEditor.Dispose()` (~`CodeEditor.cs:418-427`) only disposes `_cssBroker` and `_parentAccessor`. Missing: presenter disposal, WebView event unsubscription, WebView disposal, TS-side `editor.dispose()`, `_initialized` reset.

2. **No presenter disposal contract**: `ICodeEditorPresenter` has no `IDisposable` member — presenter cleanup is not formalized.

3. **WASM EditorContext leak** (BUG 11): `EditorContext._editors` map in `otherScriptsToBeOrganized.ts:6` is never cleaned because WASM code path never calls `disposeEditor`. Each editor instance leaks its Monaco model and view.

4. **getEditorForElement auto-creates on miss** (BUG 12): `otherScriptsToBeOrganized.ts:14-23` auto-creates an `EditorContext` when element not found, masking bugs where disposal or init didn't work correctly.

5. **Test app tab close** (BUG 14): `TabView_TabCloseRequested` in `MainPage.xaml.cs:44-47` removes tab item but never calls `Dispose()` on the `EditorControl`, leaving everything alive.

**Files**:
- `MonacoEditorComponent/CodeEditor/CodeEditor.cs` — `Dispose()` method
- `MonacoEditorComponent/CodeEditor/CodeEditor.Events.cs` — event unsubscription
- `MonacoEditorComponent/CodeEditor/CodeEditor.Methods.cs` — use-after-dispose guards on public methods
- `MonacoEditorComponent/CodeEditor/ICodeEditorPresenter.cs` — add `IDisposable` to presenter contract
- `MonacoEditorComponent/CodeEditor/WasmCodeEditorPresenter.cs` — WASM-specific cleanup, implement disposal
- `MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs` — Desktop-specific cleanup, implement disposal
- `MonacoEditorComponent/ts-helpermethods/asyncCallbackHelpers.ts` — `disposeEditor()` function (this is where disposal logic lives)
- `MonacoEditorComponent/ts-helpermethods/otherScriptsToBeOrganized.ts` — `EditorContext._editors` map cleanup, fix `getEditorForElement` auto-create
- `MonacoEditorTestApp/MainPage.xaml.cs` — `TabView_TabCloseRequested`

**Approach**:
1. Add `IDisposable` (or `IAsyncDisposable`) to `ICodeEditorPresenter` so each presenter owns its platform-specific cleanup.
2. Implement comprehensive `Dispose(bool disposing)` pattern on `CodeEditor`:
   - Unsubscribe all C# events (`Loaded`, `Unloaded`, `SizeChanged`, etc.)
   - Invoke TS `disposeEditor()` via JS interop (located in `asyncCallbackHelpers.ts`) to clean up Monaco instance
   - Dispose presenter (calls into platform-specific cleanup)
   - Dispose WebView and bridge handler
   - Set `_initialized = false`, `_disposed = true`
3. Add use-after-dispose guards to public methods in `CodeEditor.Methods.cs`
4. Fix `disposeEditor()` in `asyncCallbackHelpers.ts` to also remove from `EditorContext._editors` map (in `otherScriptsToBeOrganized.ts`)
5. Fix BUG 12: Make `getEditorForElement` throw on miss (element not found in `_editors` map). Add a separate `tryGetEditorForElement` that returns `null` for nullable lookup patterns. Callers that expect an editor must use the throwing variant; callers that handle missing editors use the try variant.
6. Add WASM-specific cleanup: call `disposeEditor()` via JS interop in `WasmCodeEditorPresenter.Dispose()`
7. Fix test app: call `Dispose()` on `EditorControl` in `TabView_TabCloseRequested`

**Key context**:
- Monaco `editor.dispose()` must be called or the editor leaks DOM nodes and event listeners — see https://github.com/microsoft/monaco-editor/issues/4702
- The `Bridge/WebView2JsonRpcMessageHandler.cs` already has a proper `Dispose()` with `_disposed` guard — follow that pattern
- `disposeEditor()` is implemented in `asyncCallbackHelpers.ts`, NOT `otherScriptsToBeOrganized.ts` — the latter only contains the `EditorContext` map helpers

## Acceptance
- [ ] `ICodeEditorPresenter` extends `IDisposable` (or `IAsyncDisposable`)
- [ ] Both presenters implement disposal with platform-specific cleanup
- [ ] `CodeEditor.Dispose()` cleans up all resources: events, presenter, WebView, JS-side Monaco instance
- [ ] Public methods in `CodeEditor.Methods.cs` guard against use-after-dispose
- [ ] WASM path calls `disposeEditor()` on cleanup
- [ ] `EditorContext._editors` map is cleaned when an editor is disposed
- [ ] BUG 12 fixed: `getEditorForElement` throws on miss; `tryGetEditorForElement` returns null for nullable lookup
- [ ] Test app `TabView_TabCloseRequested` calls `Dispose()` on the editor
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm` succeeds

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
