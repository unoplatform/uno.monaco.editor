# fn-6-bug-fixes-disposal-resize-and-test.3 Complete Dispose implementation and WASM JS cleanup

## Description

**Size**: L — disposal chain across C#, JS, and test app

**Problem**: Multiple disposal/cleanup gaps:

1. **Incomplete `Dispose()`** (BUG 4): `CodeEditor.Dispose()` (~`CodeEditor.cs:418-427`) only disposes `_cssBroker` and `_parentAccessor`. Missing: presenter disposal, WebView event unsubscription, WebView disposal, TS-side `editor.dispose()`, `_initialized` reset. Also uses `new` keyword (hides base class `IDisposable.Dispose()`), meaning calling `Dispose()` through an `IDisposable` reference invokes the base class version, not this one.

2. **No presenter disposal contract**: `ICodeEditorPresenter` has no `IDisposable` member — presenter cleanup is not formalized.

3. **WASM EditorContext leak** (BUG 11): `EditorContext._editors` map in `otherScriptsToBeOrganized.ts:6` is never cleaned because WASM code path never calls `disposeEditor`. Each editor instance leaks its Monaco model and view.

4. **Model not disposed** (BUG 11 extension): `disposeEditor()` in `asyncCallbackHelpers.ts` calls `editor.dispose()` but does NOT call `model.dispose()`. The model remains in Monaco's internal registry and leaks document content.

5. **Monaco event disposables discarded** (BUG 18): `onDidChangeContent` and `onDidChangeCursorSelection` return `IDisposable` objects that are never stored or disposed. After `disposeEditor()`, if the model survives (BUG 11), callbacks fire into stale accessor references.

6. **getEditorForElement auto-creates on miss** (BUG 12): `otherScriptsToBeOrganized.ts:14-23` auto-creates an `EditorContext` when element not found, masking bugs. Also `registerEditorForElement` silently overwrites old editor without disposing.

7. **Test app tab close** (BUG 14): `TabView_TabCloseRequested` in `MainPage.xaml.cs:44-47` removes tab item but never calls `Dispose()` on the `EditorControl`.

8. **document.body.style.overflow** (BUG 22): `asyncCallbackHelpers.ts:187` sets `document.body.style.overflow = 'hidden'` on every editor init, never restored on dispose. Affects page layout globally.

9. **BrowserHtmlElement DOM node never removed** (BUG 26): `WasmCodeEditorPresenter` creates `BrowserHtmlElement` (line 31) but never removes it from the DOM on unload/dispose.
<!-- Updated by plan-sync: fn-6.1 removed the LayoutUpdated handler entirely (replaced with ResizeObserver in TS), so the stale "LayoutUpdated handler can never be unsubscribed" concern no longer applies -->

10. **IThemeListener missing IDisposable** (BUG 27): `IThemeListener.cs` has no `IDisposable` contract despite both implementations needing cleanup.

11. **Test app EditorControl bugs** (BUGs 28-30):
    - Remove path missing Unloaded/PropertyChanged unsubscribe (`EditorControl.xaml.cs:454-465`)
    - NullRef in all button handlers after Remove (no null guard on CodeEditor)
    - TextBox OneTime binding (`EditorControl.xaml:46`) never shows loaded content

**Files**:
- `MonacoEditorComponent/CodeEditor/CodeEditor.cs` — `Dispose()` method
- `MonacoEditorComponent/CodeEditor/CodeEditor.Events.cs` — event unsubscription
- `MonacoEditorComponent/CodeEditor/CodeEditor.Methods.cs` — use-after-dispose guards on public methods
- `MonacoEditorComponent/CodeEditor/ICodeEditorPresenter.cs` — add `IDisposable` to presenter contract
- `MonacoEditorComponent/CodeEditor/WasmCodeEditorPresenter.cs` — WASM-specific cleanup, implement disposal
- `MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs` — Desktop-specific cleanup, implement disposal
- `MonacoEditorComponent/Helpers/IThemeListener.cs` — add `IDisposable` to interface
- `MonacoEditorComponent/ts-helpermethods/asyncCallbackHelpers.ts` — `disposeEditor()` function (model dispose, event cleanup, overflow restore)
- `MonacoEditorComponent/ts-helpermethods/otherScriptsToBeOrganized.ts` — `EditorContext._editors` map cleanup, fix `getEditorForElement` auto-create, fix `registerEditorForElement` overwrite
- `MonacoEditorTestApp/MainPage.xaml.cs` — `TabView_TabCloseRequested`
- `MonacoEditorTestApp/EditorControl.xaml.cs` — Remove path fixes, null guards
- `MonacoEditorTestApp/EditorControl.xaml` — Fix OneTime binding

**Approach**:
1. Add `IDisposable` (or `IAsyncDisposable`) to `ICodeEditorPresenter` so each presenter owns its platform-specific cleanup.
2. Add `IDisposable` to `IThemeListener` interface.
3. Implement comprehensive `Dispose(bool disposing)` pattern on `CodeEditor`:
   - Unsubscribe all C# events (`Loaded`, `Unloaded`, `SizeChanged`, etc.)
   - Invoke TS `disposeEditor()` via JS interop (located in `asyncCallbackHelpers.ts`) to clean up Monaco instance
   - Dispose presenter (calls into platform-specific cleanup)
   - Dispose WebView and bridge handler
   - Set `_initialized = false`, `_disposed = true`
4. Add use-after-dispose guards to public methods in `CodeEditor.Methods.cs`
5. Fix `disposeEditor()` in `asyncCallbackHelpers.ts`:
   - Dispose the model (`editorContext.model.dispose()`)
   - Store and dispose `onDidChangeContent` and `onDidChangeCursorSelection` disposables
   - Remove from `EditorContext._editors` map
   - Restore `document.body.style.overflow` if last editor
6. Fix BUG 12: Make `getEditorForElement` throw on miss. Add `tryGetEditorForElement` that returns `null` for nullable lookup patterns. Fix `registerEditorForElement` to dispose old editor before overwriting.
7. Fix WASM: call `disposeEditor()` via JS interop in `WasmCodeEditorPresenter.Dispose()`; remove `BrowserHtmlElement` from DOM.
8. Fix test app: call `Dispose()` on `EditorControl` in `TabView_TabCloseRequested`; add null guards in button handlers; fix Unloaded/PropertyChanged subscribe/unsubscribe; fix TextBox binding.

**Key context**:
- Monaco `editor.dispose()` must be called or the editor leaks DOM nodes and event listeners — see https://github.com/microsoft/monaco-editor/issues/4702
- Monaco `model.dispose()` must be called separately; `editor.dispose()` does NOT dispose the model
- The `Bridge/WebView2JsonRpcMessageHandler.cs` already has a proper `Dispose()` with `_disposed` guard — follow that pattern
- `disposeEditor()` is implemented in `asyncCallbackHelpers.ts`, NOT `otherScriptsToBeOrganized.ts`

## Required Tests

Each bug fix MUST have corresponding test(s):

- **BUG 4 test**: Verify `Dispose()` sets `_disposed = true` and `_initialized = false`
- **BUG 4 test**: Verify double-dispose is safe (no throw)
- **BUG 4 test**: Verify `Dispose()` via `IDisposable` reference calls the correct implementation (not base)
- **BUG 4 test**: Verify event handlers are unsubscribed after `Dispose()` (Loaded/Unloaded/SizeChanged fire no handler)
- **BUG 11 test**: Verify EditorContext._editors map is cleaned after disposeEditor (via C#-observable path)
- **BUG 18 test**: Verify Monaco event disposables are stored and disposed during editor cleanup
- **BUG 12 test**: Verify `getEditorForElement` throws on miss (not auto-create)
- **BUG 12 test**: Verify `tryGetEditorForElement` returns null on miss
- **BUG 12 test**: Verify `registerEditorForElement` disposes old editor if one exists
- **BUG 14 test**: Verify test app `TabView_TabCloseRequested` calls `Dispose()` on editor
- **BUG 22 test**: Verify `document.body.style.overflow` is restored after last editor disposes
- **BUG 26 test**: Verify `BrowserHtmlElement` is removed from DOM on dispose
- **BUG 27 test**: Verify `IThemeListener` implementations are disposable and cleanup resources
- **BUGs 28-29 test**: Verify EditorControl button handlers guard against null CodeEditor
- **BUG 30 test**: Verify TextBox binding updates when content is loaded
- **Regression test**: Verify use-after-dispose on public methods throws `ObjectDisposedException`

## Acceptance
- [ ] `ICodeEditorPresenter` extends `IDisposable` (or `IAsyncDisposable`)
- [ ] `IThemeListener` extends `IDisposable`
- [ ] Both presenters implement disposal with platform-specific cleanup
- [ ] `CodeEditor.Dispose()` cleans up all resources: events, presenter, WebView, JS-side Monaco instance
- [ ] `Dispose()` uses proper `override` pattern (not `new`)
- [ ] Public methods in `CodeEditor.Methods.cs` guard against use-after-dispose
- [ ] WASM path calls `disposeEditor()` on cleanup
- [ ] `disposeEditor()` disposes model and event disposables
- [ ] `EditorContext._editors` map is cleaned when an editor is disposed
- [ ] BUG 12 fixed: `getEditorForElement` throws on miss; `tryGetEditorForElement` returns null
- [ ] `registerEditorForElement` disposes old editor before overwriting
- [ ] `document.body.style.overflow` restored on dispose
- [ ] `BrowserHtmlElement` removed from DOM on dispose
- [ ] Test app `TabView_TabCloseRequested` calls `Dispose()` on the editor
- [ ] Test app EditorControl: null guards, subscribe/unsubscribe balance, binding fix
- [ ] Each bug has at least one regression test
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm` succeeds

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
