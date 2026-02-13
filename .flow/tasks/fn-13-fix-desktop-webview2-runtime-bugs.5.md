# fn-13-fix-desktop-webview2-runtime-bugs.5 Add tests for presenter lifecycle, deadlock fix, serialization, and null selection guard

## Description
The fn-13 implementation (tasks 1-3) modified 7 files with 366 insertions but added **zero tests**. Every behavioral change needs test coverage.

Changes that need tests:
1. **Presenter lifecycle reuse** (`CodeEditor.cs`): `OnApplyTemplate()` reuse path, `IsPresenterHealthy()`, deferred teardown race guard (`IsLoaded` check in `DeferredTeardownAsync`), soft-reload detection in `CodeEditor_Loaded`
2. **JSON-RPC deadlock fix** (`CodeEditor.Events.cs`): Fire-and-forget `InvokeScriptAsync` with `ContinueWith` error propagation, 30s timeout fallback for `CodeEditorLoaded`
3. **ElementTheme serialization** (`MonacoJsonContext.cs`): `ElementTheme` registered in source-gen context — no reflection fallback
4. **Null selection guard** (`updateSelectedContent.ts`): No-op on null/collapsed selection
5. **Theme init error recovery** (`asyncCallbackHelpers.ts`): Diagnostic logging, fallback behavior

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
- **Null selection guard**: Use `page.EvaluateAsync` to call `updateSelectedContent` with no selection and verify no error thrown. Or press "Set Selected Text" button via CDP and verify no crash.
- **Text loading**: Verify `Content.txt` text appears in the editor model on first load — `monaco.editor.getEditors()[0].getModel().getValue()` should be non-empty.
- **Theme consistency**: Verify Monaco theme matches OS theme on init — check that `getJsonValue("RequestedTheme")` resolves without timeout.

### Test patterns to follow
- Existing pattern in `SerializationContractTests.cs`: golden baseline with `JsonDocument.Parse` assertions
- Existing pattern in `DesktopIntegrationTests.cs`: `_fixture.Page.EvaluateAsync<T>()` for DOM verification
- Trait `[Trait("Category", "DesktopCDP")]` for desktop-only tests
- Trait `[Trait("Category", "Serialization")]` for serialization tests

## Acceptance
- [ ] `ElementTheme` serialization golden baseline test: serialize via `MonacoJsonContext.Relaxed.Options`, verify no exception, verify correct wire format
- [ ] `ElementTheme` round-trip test: serialize then deserialize, verify equality
- [ ] Desktop CDP test: editor text content is non-empty on first load (Content.txt loaded)
- [ ] Desktop CDP test: null/collapsed selection does not throw when `updateSelectedContent` is invoked
- [ ] Desktop CDP test: `getJsonValue("RequestedTheme")` resolves without timeout (theme init not deadlocked)
- [ ] All new tests pass locally and in CI
- [ ] No regressions in existing tests
- [ ] Solution builds clean for both net10.0-desktop and net10.0-browserwasm targets

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
