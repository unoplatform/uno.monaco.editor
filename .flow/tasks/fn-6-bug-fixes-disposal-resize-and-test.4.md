# fn-6-bug-fixes-disposal-resize-and-test.4 Fix data correctness bugs and error handling

## Description

**Size**: M — multiple independent fixes across C# and TS

**Problem**: Several data correctness and error handling bugs (serialized after task .2 to avoid merge conflicts on shared files):

1. **DependencyProperty type mismatch** (BUG 8): `DecorationsProperty` (~`CodeEditor.Properties.cs:174`) registered as `typeof(IModelDeltaDecoration)` but CLR property type is `IObservableVector<IModelDeltaDecoration>`. Same for `MarkersProperty` (~`CodeEditor.Properties.cs:227`). This causes silent failures when the property system tries to assign values.

2. **EOL handling** (BUG 7): `updateSelectedContent.ts:18-21` uses `\r` for line splitting instead of `\n`. Fails on non-Windows platforms (WASM in particular runs in browsers which use `\n`).

3. **Null reference in RunScriptHelperAsync** (BUG 6): `WebViewExtensions.cs:45` — `returnstring.Contains("wv_internal_error")` will throw `NullReferenceException` if `returnstring` is null.

4. **Async void exception swallowing** (BUG 9): Property changed callbacks in `CodeEditor.Properties.cs:30` use `async void` which swallows exceptions silently.

5. **SelectedText fire-and-forget** (BUG 10): `SelectedTextProperty` changed callback (~`CodeEditor.Properties.cs:60`) uses `_ =` to discard the async task, hiding failures.

**Not in scope**: BUG 5 (Desktop `AllowedFileContentRoot`) — deferred, requires fn-1 content delivery. BUG 13 (sanitize/desanitize ordering) — already verified/covered by `BridgeEncodingTests.cs`.

**Files**:
- `MonacoEditorComponent/CodeEditor/CodeEditor.Properties.cs` — property registrations, callbacks
- `MonacoEditorComponent/ts-helpermethods/updateSelectedContent.ts` — EOL fix
- `MonacoEditorComponent/Extensions/WebViewExtensions.cs` — null check

**Approach**:
1. Fix `DecorationsProperty` and `MarkersProperty` type registration to use the correct collection types
2. Replace `\r` with `\n` (or use a regex for `\r?\n` to handle both) in `updateSelectedContent.ts`
3. Add null-conditional (`?.`) check before `.Contains()` in `WebViewExtensions`
4. Replace async void with proper async Task callbacks wrapped in error handling, or add try-catch with logging
5. Do NOT touch `DesktopCodeEditorPresenter.cs` (BUG 5 deferred) or `asyncCallbackHelpers.ts` sanitize (BUG 13 already verified)

**Key context**:
- DependencyProperty type must match CLR property type exactly or the property system silently fails
- WinRT/Uno `IObservableVector<T>` is the correct type for collection dependency properties
- The `\r` vs `\n` issue is particularly impactful on WASM where the browser always uses `\n`

## Acceptance
- [ ] `DecorationsProperty` and `MarkersProperty` registered with correct collection type
- [ ] `updateSelectedContent.ts` handles both `\n` and `\r\n` line endings
- [ ] `RunScriptHelperAsync` is null-safe
- [ ] Property changed callbacks have error handling (no silent exception swallowing)
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds
- [ ] All existing tests pass

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
