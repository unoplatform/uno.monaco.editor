# fn-10-fix-reverted-emitter-docs-enable-cdp-ci.6 Enable AddActionAsync/AddCommandAsync on desktop

## Description
`AddActionAsync` and `AddCommandAsync` currently do not work on desktop. Remove the outdated `PlatformNotSupportedException` guards and fix the underlying `element` reference issue that causes eval-based `InvokeScriptAsync` calls to silently fail on desktop.

**Size:** M
**Files:** `MonacoEditorComponent/CodeEditor/CodeEditor.Methods.cs`, `MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs` (or `WebViewExtensions.cs`), possibly `MonacoEditorComponent/ts-helpermethods/index.ts`

## Root cause analysis

### Guard issue (minor)
`AddActionAsync` (CodeEditor.Methods.cs:169-185) and `AddCommandAsync` (CodeEditor.Methods.cs:258-292) throw `PlatformNotSupportedException` when `_parentAccessor is null` on non-browser. After desktop `InitialiseWebObjects()` runs (CodeEditor.Events.cs:297-306), `_parentAccessor` IS set to a `ParentAccessorDesktop` instance. So the guard only fires if called pre-init — the error message ("not yet supported on desktop") is misleading.

### `element` undefined (real blocker)
All eval-based `InvokeScriptAsync` calls build JS scripts like `methodName(element, args...)` (WebViewExtensions.cs:133). On WASM, the presenter's `InvokeScriptAsync(string)` calls `NativeMethods.InvokeJS(elementId, script)` which wraps the script in `var element = document.getElementById("${elementId}"); ${command}` (asyncCallbackHelpers.ts:335). On desktop, `DesktopCodeEditorPresenter.InvokeScriptAsync(string)` passes the raw script to `CoreWebView2.ExecuteScriptAsync` — `element` is **never defined** in the WebView2 JS global scope. Result: all eval-based calls silently fail (exceptions caught at WebViewExtensions.cs:140).

### Bridge infrastructure IS complete
- `ParentAccessorDesktop` (ParentAccessorDesktop.cs:286-304) has JSON-RPC targets: `OnCallAction`, `OnCallActionWithParameters`, `OnCallEvent`
- TS `ParentAccessor` (Monaco.Helpers.ParentAccessor.ts:61-89) routes desktop action callbacks through JSON-RPC (`parentAccessor/callAction`, `parentAccessor/callActionWithParameters`)
- JS `addAction`/`addCommand` (otherScriptsToBeOrganized.ts:80-101) use `Accessor.callAction()`/`callActionWithParameters2()` for callbacks

The plumbing is wired. Only the `element` reference in eval scripts is missing.

## Approach

### Option A: Wrap scripts in DesktopCodeEditorPresenter.InvokeScriptAsync (recommended)
Mirror what WASM's `InvokeJS` does — define `element` per-call in the desktop `InvokeScriptAsync(string script)`:
- Pattern: `var element = document.getElementById('editor-container'); {script}` (wrapped in IIFE for scope safety)
- This fixes ALL eval-based calls on desktop, not just addAction/addCommand
- The element ID is `editor-container` (from editor.html:28)
- Self-contained change in DesktopCodeEditorPresenter.cs:212-220

### Option B: Set globalThis.element after editor creation
- In the TS bundle's desktop auto-init or after `createMonacoEditor`, assign `globalThis.element = document.getElementById('editor-container')`
- Simpler but relies on single-editor-per-WebView2 assumption
- Requires TS bundle rebuild

### Either option requires:
1. **Remove PlatformNotSupportedException guards** in `AddActionAsync` and `AddCommandAsync`. Keep the `_parentAccessor is null` → `InvalidOperationException` check (applies to both platforms).
2. **Update XML doc comments** — remove "Custom actions require the WASM bridge; desktop support is not yet available" from both methods' `<exception>` docs.
3. **Verify end-to-end** — Use the desktop test app to register an action (Ctrl+Shift+P or keybinding) and confirm the C# callback fires.

### Note: broader `element` impact
The `element` undefined issue affects ALL eval-based operations on desktop (focus, layout, updateContent, updateLanguage, etc. — see CodeEditor.Events.cs:165,355,357 and CodeEditor.Events.cs:385-394). Option A would fix all of these at once. This task should prioritize fixing the mechanism, with AddAction/AddCommand as the validation target.

## Key file references
- `CodeEditor.Methods.cs:169-185` — AddActionAsync with PlatformNotSupportedException
- `CodeEditor.Methods.cs:258-292` — AddCommandAsync with PlatformNotSupportedException  
- `WebViewExtensions.cs:133` — script builder: `method + "(element," + args + ");"`
- `WebViewExtensions.cs:140` — silent catch on desktop JS errors
- `DesktopCodeEditorPresenter.cs:212-220` — desktop InvokeScriptAsync (no element wrapper)
- `WasmCodeEditorPresenter.cs:142-146` — WASM InvokeScriptAsync (calls InvokeJS with elementId)
- `asyncCallbackHelpers.ts:334-337` — InvokeJS defines element per-call
- `editor.html:28` — `<div id="editor-container">`
- `ParentAccessorDesktop.cs:286-304` — JSON-RPC targets for callAction (already working)

## Acceptance
- [ ] PlatformNotSupportedException removed from AddActionAsync and AddCommandAsync
- [ ] XML doc comments updated to reflect desktop support
- [ ] `element` is properly defined when eval-based scripts run on desktop
- [ ] `addAction(element, ...)` and `addCommand(element, ...)` execute successfully on desktop WebView2
- [ ] Action callback fires in C# when triggered from Monaco on desktop
- [ ] Existing WASM behavior unchanged (no regression)
- [ ] Solution builds without errors: `dotnet build MonacoEditorComponent.slnx`
## Completion summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
