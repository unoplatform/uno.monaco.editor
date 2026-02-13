# fn-13-fix-desktop-webview2-runtime-bugs.5 Add tests for presenter lifecycle, deadlock fix, serialization, and null selection guard

## Description
The fn-13 implementation (tasks 1-3, 6) modified 7 files with 366+ insertions but added **zero tests**. Every behavioral change needs test coverage.

Changes that need tests:
1. **Presenter lifecycle reuse** (`CodeEditor.cs`): `OnApplyTemplate()` reuse path, `IsPresenterHealthy()`, deferred teardown race guard (`IsLoaded` check in `DeferredTeardownAsync`), soft-reload detection in `CodeEditor_Loaded`
2. **JSON-RPC threading and deadlock fix** (`DesktopCodeEditorPresenter.cs`, `CodeEditor.Events.cs`, `ParentAccessor.cs`, `ParentAccessorDesktop.cs`): `JsonRpc.SynchronizationContext` set to UI thread, `HasThreadAccess` guards eliminating redundant dispatches, `ConfigureAwait(false)` for non-UI work, Fire-and-forget `InvokeScriptAsync` with `ContinueWith` error propagation, 30s timeout fallback for `CodeEditorLoaded`
3. **Initial state push architecture** (`CodeEditor.Events.cs`, `asyncCallbackHelpers.ts`): `BuildInitialStateJson()` serializes theme/text/language/readOnly, `createMonacoEditor` accepts 4th `initialStateJson` parameter, `InitialState` interface used for synchronous configuration instead of async RPC round-trips
4. **ElementTheme serialization** (`MonacoJsonContext.cs`): `ElementTheme` registered in source-gen context — no reflection fallback
5. **CSS prefers-color-scheme** (`editor.html`): Dark/light background applied before Monaco loads to prevent flash
6. **Null selection guard** (`updateSelectedContent.ts`): No-op on null/collapsed selection
7. **Theme init recovery** (`asyncCallbackHelpers.ts`): Diagnostic logging, fallback to `prefers-color-scheme` when values unavailable

**Size:** M
**Files:**
- `MonacoEditorComponent.Tests/Serialization/SerializationContractTests.cs` (ElementTheme golden baseline)
- `MonacoEditorComponent.Tests/DesktopIntegrationTests.cs` (CDP tests for lifecycle reuse, theme init)
- `MonacoEditorComponent.Tests/DesktopBridgeIntegrationTests.cs` (CDP tests for null selection, text loading)
- New or existing unit test file for `IsPresenterHealthy()` and deferred teardown logic if testable without WebView2

## Approach

### Unit tests (no WebView2 needed)
- **ElementTheme serialization**: Add a golden baseline test in `SerializationContractTests.cs` that serializes `ElementTheme.Dark` via `MonacoJsonContext.Relaxed.Options` and verifies it produces a numeric value without throwing `NotSupportedException`. Also verify round-trip deserialization.
- **IsPresenterHealthy()**: If the method is accessible via test seam or can be tested indirectly, add coverage. Otherwise document why it's only testable via integration.

### Desktop CDP integration tests (Playwright)
- **Presenter lifecycle**: Verify Monaco editor instance count stays at 1 after simulated tab-switch-like re-template. Evaluate `monaco.editor.getEditors().length` before and after. This may require a test app page with TabView — scope accordingly.
- **InitialState push**: Verify that `createMonacoEditor` is called with 4 parameters (including `initialStateJson`), and that JSON contains expected keys: `requestedTheme`, `themeName`, `isHighContrast`, `text`, `language`, `readOnly`. Use CDP console to spy on JS function or parse network logs.
- **Threading/HasThreadAccess**: Verify no deadlocks during init — `CodeEditorLoaded` completes within reasonable time and theme/text are applied. Check diagnostics for presence of `BuildInitialStateJson:` and `JsonRpc.SynchronizationContext set` messages.
- **Null selection guard**: Use `page.EvaluateAsync` to call `updateSelectedContent` with no selection and verify no error thrown. Or press "Set Selected Text" button via CDP and verify no crash.
- **Text loading**: Verify `Content.txt` text appears in the editor model on first load — `monaco.editor.getEditors()[0].getModel().getValue()` should be non-empty. This now uses pushed `InitialState.text` instead of async RPC round-trip.
- **Theme consistency**: Verify Monaco theme matches OS theme on init — CSS `prefers-color-scheme` background should match Monaco theme applied from `InitialState.themeName`. No async timeout required since theme is now pushed synchronously.

### Test patterns to follow
- Existing pattern in `SerializationContractTests.cs`: golden baseline with `JsonDocument.Parse` assertions
- Existing pattern in `DesktopIntegrationTests.cs`: `_fixture.Page.EvaluateAsync<T>()` for DOM verification
- Trait `[Trait("Category", "DesktopCDP")]` for desktop-only tests
- Trait `[Trait("Category", "Serialization")]` for serialization tests

## Acceptance
- [ ] `ElementTheme` serialization golden baseline test: serialize via `MonacoJsonContext.Relaxed.Options`, verify no exception, verify correct wire format
- [ ] `ElementTheme` round-trip test: serialize then deserialize, verify equality
- [ ] Desktop CDP test: `createMonacoEditor` invoked with 4 parameters, `initialStateJson` contains all 6 expected properties
- [ ] Desktop CDP test: editor text content is non-empty on first load (Content.txt loaded via pushed `InitialState.text`)
- [ ] Desktop CDP test: null/collapsed selection does not throw when `updateSelectedContent` is invoked
- [ ] Desktop CDP test: theme applied correctly from `InitialState.themeName` (no async round-trip, no timeout)
- [ ] Desktop CDP test: CSS `prefers-color-scheme` background matches Monaco theme from frame 0 (no flash)
- [ ] Unit test: `BuildInitialStateJson()` returns valid JSON with all required properties
- [ ] Unit test or integration: `JsonRpc.SynchronizationContext` set to UI thread (verify diagnostics or behavior)
- [ ] All new tests pass locally and in CI
- [ ] No regressions in existing tests
- [ ] Solution builds clean for both net10.0-desktop and net10.0-browserwasm targets
- [ ] <!-- Updated by plan-sync: fn-13.6 pushed InitialState to reduce async RPC round-trips during theme/text init -->

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
