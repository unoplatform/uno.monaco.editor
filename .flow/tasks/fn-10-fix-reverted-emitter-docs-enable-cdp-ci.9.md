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
- Add markers via `SetModelMarkersAsync`, verify they appear in Monaco marker data
- Set decorations via `Decorations` observable collection, verify CSS styles injected
- Verify marker data roundtrips (severity, message, position)

**Code folding:**
- Load multi-line content with foldable regions, verify folding ranges exist via Monaco API
- Verify fold/unfold operations work

**Editor options:**
- Set `ReadOnly = true`, verify editor is read-only in Monaco
- Toggle `HasGlyphMargin`, verify DOM glyph margin appears/disappears

**Size:** M
**Files:**
- `MonacoEditorComponent.Tests/DesktopBridgeIntegrationTests.cs` (new test class)
- `MonacoEditorComponent.Tests/DesktopAppFixture.cs` (add `ResetEditorStateAsync()` helper; cursor-based log API already added in task 8)
- `MonacoEditorTestApp/MainPage.xaml.cs` (add test command/action registration with stdout logging after `EditorLoaded`)

## Approach
- Create a new test class `DesktopBridgeIntegrationTests` in the same collection ("DesktopCDP") sharing `DesktopAppFixture`

### C# → JS direction (property/method set from C# side, verify in Monaco JS):
- **Bridge-driven property sets:** Use `parentAccessor/setValue` via `page.EvaluateAsync()` to set properties on the C# side, then read Monaco state from JS. This validates the bridge dispatch path.
- **Host-initiated property set (at least one required):** The test app sets `CodeEditor.Text` and `CodeEditor.CodeLanguage` from C# in its `EditorLoaded` handler to known test values (e.g., `Text = "// test-init-text"`, `CodeLanguage = "javascript"`). The test app emits `Console.WriteLine("TEST_INIT_PROPS:text=// test-init-text,lang=javascript")` (gated by `MONACO_DIAGNOSTICS`). Tests verify these host-initiated C# property sets arrived in Monaco via `page.EvaluateAsync("monaco.editor.getModels()[0].getValue()")` and `getLanguageId()`. This validates the true C# DP → SendScriptAsync → JS path without any bridge simulation.
- For C# methods like `AddCommandAsync`/`AddActionAsync`, the test app calls these from C# (see "C# method invocation" section below).

### JS → C# direction (simulate JS bridge calls, verify C# state):
- Send bridge notifications via `page.EvaluateAsync("window.__jsonRpc.sendNotification('parentAccessor/setValue', { name: 'Text', value: '\"new text\"' })")` — this triggers the C# `OnSetValue` handler which updates the `Text` DP
- Read C# state back via `page.EvaluateAsync("window.__jsonRpc.sendRequest('parentAccessor/getJsonValue', { name: 'Text' })")` — this calls the C# `OnGetJsonValue` handler

### C# method invocation for AddCommandAsync/AddActionAsync (LOCKED):

**How `AddCommandAsync` works internally:**
1. `AddCommandAsync(keybinding, handler, context)` calls `_parentAccessor.RegisterActionWithParameters(name, callback)` where name = `"Command{N}"` (see `CodeEditor.Methods.cs:253`)
2. Then calls `InvokeScriptAsync("addCommand", [keybinding, name, context])` which sends JS to Monaco to register the command
3. When JS triggers the command, Monaco calls `parentAccessor/callActionWithParameters` with `{ name: "Command1", parameters: [...] }` — this dispatches to the registered C# callback

**How `AddActionAsync` works internally:**
1. `AddActionAsync(action)` calls `_parentAccessor.RegisterAction("Action" + action.Id, callback)` (see `CodeEditor.Methods.cs:176`)
2. Then calls `InvokeScriptAsync("addAction", action)` which sends JS to Monaco to register the action
3. When JS triggers the action, Monaco calls `parentAccessor/callAction` with `{ name: "ActionMyId" }` — this dispatches to the registered C# callback

**Test approach — stdout-based callback verification (no library changes, no allowlist changes, no reflection hacks):**
1. In `MonacoEditorTestApp/MainPage.xaml.cs`, after `EditorLoaded`:
   - Call `editor.AddCommandAsync(0, handler)` where handler writes `Console.WriteLine($"TEST_CALLBACK:{commandId}:invoked")`. Store the returned command ID (e.g., `commandId = "Command1"` — but tests never hardcode this).
   - Call `editor.AddActionAsync(descriptor)` where the `Run` callback writes `Console.WriteLine($"TEST_CALLBACK:Action{actionId}:invoked")`. The action ID is known (e.g., `actionId = "testAction"`, so the token is `ActiontestAction`).
   - Write `Console.WriteLine("TEST_HARNESS:commandId={commandId},actionId=testAction")` to stdout so the test can parse the registered IDs.
2. Tests read the `TEST_HARNESS:` line from captured process stdout to get the command ID.
3. Tests trigger the command from JS: `page.EvaluateAsync("editor.trigger('test', '{commandId}')")`.
4. Tests trigger the action from JS: `page.EvaluateAsync("editor.getAction('testAction').run()")`.
5. Tests verify callback fired by checking captured process stdout for `TEST_CALLBACK:{parsedCommandId}:invoked` and `TEST_CALLBACK:Action{actionId}:invoked` lines (derived from parsed IDs, never hardcoded).
6. No custom RPC endpoints, no allowlist changes, no library modifications.

**Why this works in Release:** `Console.WriteLine` outputs to process stdout in all configurations. `DesktopAppFixture` already captures stdout via `Process.StandardOutput`.

**Files changed in test app:** `MonacoEditorTestApp/MainPage.xaml.cs` (add test command/action registration with stdout logging after `EditorLoaded`).

### Test independence and log scoping:
- Add `ResetEditorStateAsync()` helper to `DesktopAppFixture` that resets text, language, theme, and markers to known defaults via `page.EvaluateAsync()`. The defaults match the host-initiated startup values (`Text = "// test-init-text"`, `CodeLanguage = "javascript"`), so after reset the editor is in the same state as after initial host-initiated setup.
- **Host-initiated property verification:** The `HostInitiatedProperties_SetFromCSharp` test uses cursor-based log API to find the `TEST_INIT_PROPS:text=// test-init-text,lang=javascript` marker in stdout (proving the C# `EditorLoaded` path executed), then verifies the corresponding Monaco values via `page.EvaluateAsync()`. The marker assertion is the primary proof that the C# DP → SendScriptAsync → JS path ran; the Monaco value check is secondary confirmation. This test is order-independent because it asserts on the marker emitted at startup (always present in stdout regardless of when the test runs). All other tests call `ResetEditorStateAsync()` at their start.
- Each test calls reset at the start (not teardown) — fail-fast if reset itself fails
- **Cursor-based log assertions:** `GetLogCursor()` → returns current position in captured stdout. `WaitForLogLineAfterAsync(cursor, pattern, timeout)` → waits for a matching line after cursor. `GetLinesAfter(cursor)` → returns all lines after cursor. This ensures per-test scoping: each test captures a cursor before its action, then only asserts on lines after that cursor (no false positives from prior tests).
- **Startup cursor strategy:** One-time startup markers (`TEST_INIT_PROPS`, `TEST_HARNESS`, `INIT_COMPLETE`) are emitted once during app launch. Tests that need these markers search from cursor `0` (beginning of stdout), not from `GetLogCursor()`. Per-action markers (`TEST_CALLBACK`) use `GetLogCursor()` captured immediately before the triggering action.
- **Per-test correlation:** `TEST_CALLBACK:` lines include the command/action name (derived from parsed IDs), so tests match on the specific name they registered. Shared fixture + cursor = no cross-test pollution.

- Keep existing `DesktopIntegrationTests` (JS-only tests) as a separate layer

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
- [ ] Host-initiated property proof: test asserts `TEST_INIT_PROPS:text=// test-init-text,lang=javascript` marker exists in stdout (searched from cursor 0, not current cursor — startup markers are one-time emissions). This is the primary proof that the C# `EditorLoaded` DP→SendScriptAsync→JS path executed. Monaco value verification (`getValue()`, `getLanguageId()`) is secondary confirmation.
- [ ] Text property roundtrip: bridge-driven (`parentAccessor/setValue`) AND host-initiated (verified via `TEST_INIT_PROPS` marker + Monaco `getValue()`)
- [ ] CodeLanguage roundtrip: host-initiated (verified via `TEST_INIT_PROPS` marker + Monaco `getLanguageId()`), AND bridge-driven language switch
- [ ] Custom language registration: register a language association via `Languages` helper, verify it's available in `getLanguages()`
- [ ] AddCommandAsync: test app registers command via C# `AddCommandAsync` in `EditorLoaded`, logs command ID to stdout (`TEST_HARNESS:commandId={id}`). Test parses the actual ID from stdout, triggers from JS via `editor.trigger('test', parsedCommandId)`, then `fixture.WaitForLogLineAfterAsync(cursor, $"TEST_CALLBACK:{parsedCommandId}:invoked")` to verify callback fired. No hardcoded IDs — tests use whatever ID `AddCommandAsync` returns.
- [ ] AddActionAsync: test app registers action via C# `AddActionAsync` in `EditorLoaded` with known ID (e.g., `actionId = "testAction"`). Callback logs `Console.WriteLine($"TEST_CALLBACK:Action{actionId}:invoked")`. Test parses `actionId` from `TEST_HARNESS:` line, triggers from JS via `editor.getAction('{actionId}').run()`, then `WaitForLogLineAfterAsync(cursor, $"TEST_CALLBACK:Action{actionId}:invoked")`. No hardcoded callback tokens — tests derive all tokens from parsed IDs
- [ ] Test harness in `MonacoEditorTestApp/MainPage.xaml.cs` uses `Console.WriteLine` for callback logging (works in Release). No custom RPC endpoints, no allowlist changes, no library modifications
- [ ] Theme switching: set theme via bridge, verify DOM class changes
- [ ] Syntax highlighting: verify CSS token classes present after setting a language
- [ ] Markers: set via bridge, verify they appear in Monaco marker data
- [ ] Decorations: set via bridge, verify CSS styles injected in DOM
- [ ] Code folding: load foldable content, verify folding ranges exist
- [ ] ReadOnly: toggle via bridge, verify editor readOnly state in Monaco
- [ ] HasGlyphMargin: toggle from C#, verify DOM reflects glyph margin presence
- [ ] SelectedText roundtrip: set selection from C# side, verify in JS; set from JS, verify in C#
- [ ] All existing DesktopIntegrationTests (JS-only) continue to pass
- [ ] All tests tagged with `[Trait("Category", "DesktopCDP")]`
- [ ] Tests are independent and do not depend on execution order
- [ ] Fixture includes reset helper for test independence (reset text, language, theme between tests)
- [ ] Tests use fixture's cursor-based log API (added in task 8) for per-test stdout assertion scoping
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
