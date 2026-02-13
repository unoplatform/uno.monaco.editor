# fn-10-fix-reverted-emitter-docs-enable-cdp-ci.8 Fix desktop bridge initialization bugs and lifecycle flickering

## Description
The desktop test app has three critical bugs visible in the debug output:

### Bug 1: JsonRpc "arguments do not match" for all bridge methods
The debug output shows repeated warnings:
```
JsonRpc Warning: 5 : Invocation of "bridge/ready" cannot occur because arguments do not match any registered target methods.
JsonRpc Warning: 5 : Invocation of "parentAccessor/setValue" cannot occur because arguments do not match any registered target methods.
JsonRpc Warning: 5 : Invocation of "parentAccessor/setValueWithType" cannot occur because arguments do not match any registered target methods.
JsonRpc Warning: 5 : Invocation of "parentAccessor/getJsonValue" cannot occur because arguments do not match any registered target methods.
```

The JS side (vscode-jsonrpc) sends named params as top-level fields:
`{ name: "Text", value: "hello" }` for `parentAccessor/setValue`

But the C# `[JsonRpcMethod]` handlers expect a single typed parameter wrapping those fields:
`OnSetValue(SetValueParams p)` where `SetValueParams(string Name, JsonElement Value)`

StreamJsonRpc's named-params dispatch tries to match top-level JSON fields to C# method parameter names. It looks for a parameter named `name` (string) and `value` (JsonElement), not a single parameter `p` (SetValueParams). This is the root cause of EVERY bridge method failing.

**Root cause**: The `[JsonRpcMethod]` handler signatures use single typed record params instead of individual parameters matching the JSON field names.

### Bug 2: Editor Loaded/Unloaded rapid cycling (flickering)
On tab switch, the debug output shows dozens of rapid `Editor_Unloaded` → `CodeEditor_Loaded` cycles. Each cycle:
- Sets `_initialized = false`
- Calls `TeardownWebObjects()` 
- Nulls `_model`
- Re-enters `CodeEditor_Loaded` which recreates everything

This causes the entire Monaco editor to be destroyed and recreated on each tab switch, causing visible flickering and loss of state. The Uno XAML lifecycle fires Unloaded/Loaded when controls move in the visual tree (tab switching), but the editor treats each transition as if it's being permanently removed.

### Bug 3: "before initialized" warning spam
Many calls (`updateStyle`, `updateDecorations`, `getLanguages`, `addCommand`, etc.) execute before the Monaco bridge is ready. The code guards with `if (_initialized && _view is not null)` but properties are set during the XAML load cycle before initialization completes.

**Size:** M
**Files:** `MonacoEditorComponent/Helpers/ParentAccessorDesktop.cs`, `MonacoEditorComponent/Bridge/BridgeContracts.cs`, `MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs`, `MonacoEditorComponent/CodeEditor/CodeEditor.cs`, `MonacoEditorComponent/Helpers/ThemeListenerDesktop.cs`, `MonacoEditorComponent/Helpers/KeyboardListenerDesktop.cs`, `MonacoEditorComponent/Helpers/DebugLoggerDesktop.cs`

## Approach

**Bug 1 fix — two options (investigate both):**
- Option A: Change `[JsonRpcMethod]` signatures to use individual parameters: `OnSetValue(string name, JsonElement value)` instead of `OnSetValue(SetValueParams p)`. This matches StreamJsonRpc's named-params dispatch.
- Option B: Keep typed params but configure StreamJsonRpc to deserialize the entire params object as a single positional arg. Check if there's a StreamJsonRpc option for this.
- Verify the fix by running existing `JsonRpcTargetDispatchTests` and `JsonRpcWireCompatibilityTests`

**Bug 2 fix:**
- The `CodeEditor_Unloaded` handler at `CodeEditor.cs:243` aggressively resets state. For tab-switch scenarios, the editor should survive Unloaded/Loaded cycles without full teardown.
- Consider: skip teardown if the control will be re-loaded shortly (debounce or check `IsLoaded` before tearing down)
- Or: preserve the presenter and WebView2 across unload/load cycles; only teardown on Dispose

**Bug 3 fix:**
- Queue operations that arrive before initialization into a pending queue
- Replay the queue once `_initialized` becomes true
- Or: defer property change callbacks until after initialization completes

## Key context
- `ParentAccessorDesktop.cs:271-310` — all `[JsonRpcMethod]` handlers with typed record params
- `BridgeContracts.cs:22-45` — the DTO records (SetValueParams, etc.)
- `CodeEditor.cs:243-268` — CodeEditor_Unloaded handler that aggressively tears down
- `CodeEditor.cs:345-421` — SendScriptAsync guards that log "before initialized" warnings
- `DesktopCodeEditorPresenter.cs:619-648` — BridgeHandshakeTarget with bridge/ready handler
- Overlaps with fn-6.2 (initialization race condition) — this task addresses the symptoms needed for testing; fn-6.2 can do a deeper architectural fix later
- StreamJsonRpc named params dispatch: https://github.com/microsoft/vs-streamjsonrpc/issues/48
## Acceptance
- [ ] All `parentAccessor/*` and `bridge/ready` JsonRpc methods dispatch correctly (no "arguments do not match" warnings)
- [ ] Property roundtrip works: setting Text from JS arrives in C#, setting CodeLanguage from C# arrives in JS
- [ ] Tab switching does not cause rapid Editor_Unloaded/CodeEditor_Loaded cycling
- [ ] Editor preserves its state (text content, language, theme) across tab switches
- [ ] "Tried to call X before initialized" warnings eliminated or significantly reduced
- [ ] Existing unit tests pass: `JsonRpcTargetDispatchTests`, `JsonRpcWireCompatibilityTests`, `BridgeEncodingTests`
- [ ] Desktop test app launches, shows text content, applies theme, and displays syntax highlighting
- [ ] App exit code is 0 (not 0xffffffff) on clean shutdown
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
