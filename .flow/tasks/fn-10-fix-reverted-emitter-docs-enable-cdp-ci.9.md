# fn-10-fix-reverted-emitter-docs-enable-cdp-ci.9 Add comprehensive C# bridge integration tests via Desktop CDP

## Description
The current DesktopIntegrationTests only test Monaco JavaScript APIs directly via `page.EvaluateAsync`. They bypass the C# bridge entirely. The test app class comment even says "the test app does the C# integration" — but that's manual testing, not automated CI.

The user requires **comprehensive automated testing through the C# bridge** to verify that the CodeEditor C# API correctly proxies to Monaco JS and that results flow back. The test app must NOT be relied upon as the sole validation of correctness.

### Testing layers needed (from the user):
1. **Bridge protocol unit tests** — test JsonRpc message dispatch in isolation (partially covered by existing `JsonRpcTargetDispatchTests`, `JsonRpcWireCompatibilityTests`, `BridgeEncodingTests`; may need expansion)
2. **JavaScript integration tests** — existing `DesktopIntegrationTests` that test Monaco JS API directly (already exist, keep them)
3. **C# bridge integration tests** — NEW: test the full C#↔JS roundtrip through the bridge protocol, verifying that C# CodeEditor properties and methods work end-to-end on desktop

### Test scenarios for layer 3 (C# bridge integration):

**Text & content:**
- Set `Text` property from C# side, verify it arrives in Monaco (read back via JS `getValue()`)
- Set text from JS side, verify C# `Text` property updates via bridge notification
- Verify `SelectedText` property roundtrips

**Language detection & registration:**
- Set `CodeLanguage = "csharp"`, verify Monaco recognizes it as C# (check `getModel().getLanguageId()`)
- Set `CodeLanguage = "xml"`, verify language switch
- Register a custom language association (e.g., `.csproj` files treated as XML) via `Languages` helper
- Verify `getLanguages()` returns available languages including registered ones

**Actions & commands:**
- Register a command via `AddCommandAsync`, trigger it from JS, verify C# callback fires
- Register an action via `AddActionAsync`, verify it appears in command palette data
- Test custom action callback roundtrip (JS triggers action → C# handler executes)

**Theme & styling:**
- Set theme via `CodeEditorTheme` property from C#, verify DOM reflects the change
- Verify syntax highlighting CSS classes are present after language is set

**Decorations & markers:**
- Add markers via `SetModelMarkersAsync`, verify they appear in Monaco
- Set decorations via `Decorations` observable collection, verify CSS styles injected
- Verify marker data roundtrips (severity, message, position)

**Code folding:**
- Load multi-line content with foldable regions, verify folding ranges exist via Monaco API
- Verify fold/unfold operations work

**Editor options:**
- Set `ReadOnly = true`, verify editor is read-only in Monaco
- Toggle `HasGlyphMargin`, verify DOM glyph margin appears/disappears

**Size:** M
**Files:** `MonacoEditorComponent.Tests/DesktopIntegrationTests.cs` (expand or create new file `DesktopBridgeIntegrationTests.cs`), `MonacoEditorComponent.Tests/DesktopAppFixture.cs` (may need helper methods)

## Approach
- Create a new test class `DesktopBridgeIntegrationTests` in the same collection ("DesktopCDP") sharing `DesktopAppFixture`
- Tests use the Playwright CDP `page.EvaluateAsync` to READ from Monaco JS (verify C# → JS direction)
- Tests use the Playwright CDP `page.EvaluateAsync` to WRITE via the bridge JS API (`window.__jsonRpc.sendNotification(...)`) to verify JS → C# direction
- For C# property reads, verify via bridge request: `window.__jsonRpc.sendRequest('parentAccessor/getJsonValue', { name: 'Text' })`
- Keep existing `DesktopIntegrationTests` (JS-only tests) as a separate layer
- Add helper methods to `DesktopAppFixture` if needed for common bridge interaction patterns
- Tests should be independent and reset editor state between tests

## Key context
- `CodeEditor.Properties.cs` — all dependency properties (Text, CodeLanguage, SelectedText, ReadOnly, HasGlyphMargin, Options, Decorations, Markers)
- `CodeEditor.Methods.cs` — public async methods (AddCommandAsync, AddActionAsync, CreateContextKeyAsync, GetPositionAsync, SetPositionAsync, GetModelMarkersAsync, SetModelMarkersAsync, RevealLineAsync, etc.)
- `CodeEditor/CodeEditor.Events.cs` — events (EditorLoading, EditorLoaded, OpenLinkRequested, KeyDown)
- `Monaco/LanguagesHelper.cs` — language registration APIs
- `DesktopContent/uno-monaco-helpers.js:219055-219130` — JS bridge methods for parentAccessor (getJsonValue, setValue, setValueWithType, callAction, callEvent)
- The existing `DesktopIntegrationTests` (JS-only) should remain as a separate test class
- Tests run in the "DesktopCDP" collection so they share the fixture (single app launch)
- User explicitly said: "it may not need to test every single api, as that's a lot, but it needs to test the major features to make sure things proxy and work right"
## Acceptance
- [ ] New `DesktopBridgeIntegrationTests` class exists with C# bridge roundtrip tests
- [ ] Text property roundtrip: C# set → JS verify AND JS set → C# verify (via bridge)
- [ ] CodeLanguage roundtrip: set "csharp" from C#, verify Monaco `getLanguageId()` returns "csharp"
- [ ] Custom language registration: register a language association, verify it's available
- [ ] AddCommandAsync: register command from C#, trigger from JS, verify callback
- [ ] AddActionAsync: register action from C#, verify it's registered in Monaco
- [ ] Theme switching: set theme via bridge, verify DOM class changes
- [ ] Syntax highlighting: verify CSS token classes present after setting a language
- [ ] Markers: set via bridge, verify they appear in Monaco marker data
- [ ] Decorations: set via bridge, verify CSS styles injected in DOM
- [ ] Code folding: load foldable content, verify folding ranges exist
- [ ] ReadOnly: toggle via bridge, verify editor readOnly state in Monaco
- [ ] All existing DesktopIntegrationTests (JS-only) continue to pass
- [ ] All tests tagged with `[Trait("Category", "DesktopCDP")]`
- [ ] Tests are independent and do not depend on execution order
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
