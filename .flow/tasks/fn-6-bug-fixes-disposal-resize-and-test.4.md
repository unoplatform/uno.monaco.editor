# fn-6-bug-fixes-disposal-resize-and-test.4 Fix data correctness bugs, threading violations, and error handling

## Description

**Size**: L — multiple independent fixes across C#, TS, and desktop bridge

**Problem**: Several data correctness, threading, and error handling bugs (serialized after task .2 to avoid merge conflicts on shared files):

1. **DependencyProperty type mismatch** (BUG 8): `DecorationsProperty` (~`CodeEditor.Properties.cs:174`) registered as `typeof(IModelDeltaDecoration)` but CLR property type is `IObservableVector<IModelDeltaDecoration>`. Same for `MarkersProperty` (~`CodeEditor.Properties.cs:227`). This causes silent failures when the property system tries to assign values.

2. **EOL handling** (BUG 7): `updateSelectedContent.ts:18-21` uses `\r` for line splitting instead of `\n`. Fails on non-Windows platforms (WASM in particular runs in browsers which use `\n`). End column calculation also wrong (`lastIndexOf('\r')` returns -1, producing off-by-one).

3. **Null reference in RunScriptHelperAsync** (BUG 6): `WebViewExtensions.cs:45` — `returnstring.Contains("wv_internal_error")` will throw `NullReferenceException` if `returnstring` is null.

4. **Async void exception swallowing** (BUG 9): Property changed callbacks in `CodeEditor.Properties.cs:30` use `async void` which swallows exceptions silently.

5. **SelectedText fire-and-forget** (BUG 10): `SelectedTextProperty` changed callback (~`CodeEditor.Properties.cs:60`) uses `_ =` to discard the async task, hiding failures.

6. **GetJsonValue off UI thread** (BUG 15): `ParentAccessorDesktop.GetJsonValue` (~line 109-135) is a `[JsonRpcMethod]` that reads DependencyProperties synchronously. StreamJsonRpc dispatches incoming requests on its own thread, not the UI thread. Reading DependencyProperties from a non-UI thread throws `InvalidOperationException` on WinUI/Uno. This is called during editor initialization (`asyncCallbackHelpers.ts:167` calls `getJsonValueAsync("RequestedTheme")`).

7. **KeyboardListenerDesktop off UI thread** (BUG 16): `KeyboardListenerDesktop.OnKeyDown` (~line 43-47) is a `[JsonRpcMethod]` that fires `KeyDown` events. User-registered C# event handlers will run on the JSON-RPC dispatch thread, not the UI thread. Any UI-touching code in handlers will throw.

8. **ManagedCallEvent double-desanitize** (BUG 19): `ParentAccessor.wasm.cs:118-129` calls `Desanitize(resultString)` on the C# side, then `callParentEventAsync` in TS calls `desanitize(result)` again. Double-desanitization corrupts values containing percent-encoded patterns.

9. **ManagedSetValue destructive string processing** (BUG 20): `ParentAccessor.wasm.cs:28-44` applies `Replace(@"\\\\", @"\\")`, `Trim('"')`, and `Replace(@"\r\n", ...)` after desanitization. `Trim('"')` strips ALL leading/trailing quotes (not just one pair). Backslash collapse can corrupt data.

10. **getThemeIsHighContrast always false** (BUG 21): `otherScriptsToBeOrganized.ts:235` compares `boolean` to string `"true"` — `true == "true"` is `false` in JavaScript. High contrast detection broken for WASM.

11. **getSrc/setSrc non-existent** (BUG 23): `WasmCodeEditorPresenter.cs:151-155` declares `[JSImport("globalThis.getSrc")]` and `[JSImport("globalThis.setSrc")]` but neither function is defined in TS or exported to `globalThis`. Will throw "not a function" if called.

12. **DropOldest drops JSON-RPC messages** (BUG 24): `WebView2JsonRpcMessageHandler.cs:65` uses `BoundedChannelFullMode.DropOldest`. Under load, oldest messages (which may be pending requests) are silently dropped, causing JS-side hangs.

13. **PostWebMessage off UI thread** (BUG 25): `WebView2JsonRpcMessageHandler.WriteAsync` calls `PostWebMessage` → `CoreWebView2.PostWebMessageAsJson`. CoreWebView2 methods require UI thread. StreamJsonRpc may call `WriteAsync` from thread pool.

**Not in scope**: BUG 5 (Desktop `AllowedFileContentRoot`) — deferred, requires fn-1 content delivery. BUG 13 (sanitize/desanitize ordering) — already verified/covered by `BridgeEncodingTests.cs`.

**Files**:
- `MonacoEditorComponent/CodeEditor/CodeEditor.Properties.cs` — property registrations, callbacks
- `MonacoEditorComponent/ts-helpermethods/updateSelectedContent.ts` — EOL fix
- `MonacoEditorComponent/Extensions/WebViewExtensions.cs` — null check
- `MonacoEditorComponent/Helpers/ParentAccessorDesktop.cs` — `GetJsonValue` UI thread marshaling
- `MonacoEditorComponent/Helpers/KeyboardListenerDesktop.cs` — `OnKeyDown` UI thread marshaling
- `MonacoEditorComponent/Helpers/ParentAccessor.wasm.cs` — `ManagedCallEvent` desanitize fix, `ManagedSetValue` string processing fix
- `MonacoEditorComponent/ts-helpermethods/otherScriptsToBeOrganized.ts` — `getThemeIsHighContrast` fix
- `MonacoEditorComponent/ts-helpermethods/asyncCallbackHelpers.ts` — remove double-desanitize in `callParentEventAsync`
- `MonacoEditorComponent/CodeEditor/WasmCodeEditorPresenter.cs` — remove dead `getSrc`/`setSrc` or implement them
- `MonacoEditorComponent/Bridge/WebView2JsonRpcMessageHandler.cs` — channel policy, `WriteAsync` thread safety

**Approach**:
1. Fix `DecorationsProperty` and `MarkersProperty` type registration to use the correct collection types
2. Replace `\r` with `\n` (or use regex `\r?\n|\r` to handle all) in `updateSelectedContent.ts`. Fix end column calculation.
3. Add null-conditional (`?.`) check before `.Contains()` in `WebViewExtensions`
4. Replace async void with proper async Task callbacks wrapped in error handling, or add try-catch with logging
5. Fix `GetJsonValue` to use `await _queue.EnqueueAsync(...)` for UI thread marshaling (make it async Task<string>, or use synchronous `DispatcherQueue.TryEnqueue` with a `TaskCompletionSource`)
6. Fix `KeyboardListenerDesktop.OnKeyDown` to marshal onto UI thread via `_queue.EnqueueAsync`
7. Remove duplicate `desanitize` call — either in C# `ManagedCallEvent` or in TS `callParentEventAsync`, not both
8. Fix `ManagedSetValue` string processing — remove destructive `Trim('"')` and backslash replacements, or scope them precisely
9. Fix `getThemeIsHighContrast` — compare boolean directly or use `=== true`
10. Remove or implement `getSrc`/`setSrc` — if dead code, remove the JSImport declarations
11. Change channel to `BoundedChannelFullMode.Wait` or increase capacity with logging on drop
12. Marshal `WriteAsync`'s `PostWebMessage` call onto UI thread

## Required Tests

Each bug fix MUST have corresponding test(s):

- **BUG 8 test**: Verify `DecorationsProperty` accepts `IObservableVector<IModelDeltaDecoration>` values without error
- **BUG 8 test**: Verify `MarkersProperty` accepts correct collection type
- **BUG 7 test**: Verify `updateSelectedContent` correctly calculates end line/column with `\n`, `\r\n`, and `\r` content (via C#-observable path)
- **BUG 6 test**: Verify `RunScriptHelperAsync` handles null return value without NullReferenceException
- **BUG 9 test**: Verify property changed callbacks propagate exceptions (not swallowed by async void)
- **BUG 10 test**: Verify `SelectedTextProperty` changed callback surfaces errors
- **BUG 15 test**: Verify `GetJsonValue` executes on UI thread (mock DispatcherQueue test)
- **BUG 16 test**: Verify `KeyboardListenerDesktop.OnKeyDown` marshals to UI thread before firing events
- **BUG 19 test**: Verify `ManagedCallEvent` return value is desanitized exactly once (round-trip test with percent-encoded characters)
- **BUG 20 test**: Verify `ManagedSetValue` preserves quotes, backslashes, and whitespace in values
- **BUG 21 test**: Verify `getThemeIsHighContrast` returns `true` when high contrast is enabled (via C#-observable path)
- **BUG 23 test**: Verify `getSrc`/`setSrc` are either removed or functional
- **BUG 24 test**: Verify bounded channel does not silently drop messages (stress test with many rapid messages)
- **BUG 25 test**: Verify `PostWebMessage` is called on UI thread (mock test)
- **Regression test**: BridgeEncoding round-trip with edge-case characters (`%`, `\`, `"`, `'`, `\r\n`)

## Acceptance
- [ ] `DecorationsProperty` and `MarkersProperty` registered with correct collection type
- [ ] `updateSelectedContent.ts` handles `\n`, `\r\n`, and `\r` line endings correctly
- [ ] `RunScriptHelperAsync` is null-safe
- [ ] Property changed callbacks have error handling (no silent exception swallowing)
- [ ] `GetJsonValue` marshals to UI thread
- [ ] `KeyboardListenerDesktop.OnKeyDown` marshals to UI thread
- [ ] `ManagedCallEvent` desanitizes exactly once
- [ ] `ManagedSetValue` preserves data integrity
- [ ] `getThemeIsHighContrast` returns correct boolean value
- [ ] `getSrc`/`setSrc` resolved (removed or implemented)
- [ ] Channel drop policy improved
- [ ] `PostWebMessage` called on UI thread
- [ ] Each bug has at least one regression test
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds
- [ ] All existing tests pass

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
