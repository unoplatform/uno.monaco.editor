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
**Files:** `MonacoEditorComponent/Helpers/ParentAccessorDesktop.cs`, `MonacoEditorComponent/Bridge/BridgeContracts.cs`, `MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs`, `MonacoEditorComponent/CodeEditor/CodeEditor.cs`, `MonacoEditorComponent/Helpers/ThemeListenerDesktop.cs`, `MonacoEditorComponent/Helpers/KeyboardListenerDesktop.cs`, `MonacoEditorComponent/Helpers/DebugLoggerDesktop.cs`, `MonacoEditorComponent/DesktopContent/bridge-protocol.md`, `MonacoEditorComponent.Tests/DesktopAppFixture.cs` (add cursor-based log query API)

## Approach

**Bug 1 fix — Strategy A (LOCKED):**
- Change ALL `[JsonRpcMethod]` handler signatures from single typed record params to individual named parameters matching the JSON field names:
  - `OnSetValue(SetValueParams p)` → `OnSetValue(string name, JsonElement value)`
  - `OnSetValueWithType(SetValueWithTypeParams p)` → `OnSetValueWithType(string name, JsonElement value, string typeName)`
  - `OnCallAction(CallActionParams p)` → `OnCallAction(string name)`
  - `OnCallActionWithParameters(CallActionWithParametersParams p)` → `OnCallActionWithParameters(string name, JsonElement parameters)`
  - `OnCallEvent(CallEventParams p)` → `OnCallEvent(string name, JsonElement parameters)`
  - `OnGetJsonValue(GetJsonValueParams p)` → `OnGetJsonValue(string name)`
  - `OnBridgeReady(BridgeReadyParams p)` → `OnBridgeReady(int protocolVersion)`
  - `OnEditorReady(EditorReadyParams p)` → `OnEditorReady(int protocolVersion)`
  - `OnLog(LogParams p)` → `OnLog(string level, string message)` (in `DebugLoggerDesktop.cs`)
  - `OnKeyDown(KeyDownParams p)` → `OnKeyDown(int keyCode, bool ctrlKey, bool shiftKey, bool altKey, bool metaKey)` (in `KeyboardListenerDesktop.cs`)
  - `OnGetThemeProperty(GetThemePropertyParams p)` → `OnGetThemeProperty(string name)` (in `ThemeListenerDesktop.cs`)
- Remove ALL unused DTO records from `BridgeContracts.cs` (SetValueParams, SetValueWithTypeParams, CallActionParams, CallActionWithParametersParams, CallEventParams, GetJsonValueParams, BridgeReadyParams, EditorReadyParams, LogParams, KeyDownParams, GetThemePropertyParams). Keep only `LifecycleUpdateParams` (C#→JS, still used).
- Update `JsonRpcTargetDispatchTests` and `JsonRpcWireCompatibilityTests` to match new signatures
- Verify with existing + updated tests

**Bug 2 fix — Deferred teardown with cancellation (LOCKED):**
- Add a `CancellationTokenSource? _unloadCts` field to `CodeEditor`
- `CodeEditor_Unloaded`: create CTS, schedule deferred teardown (e.g., `Task.Delay(100, ct)` then teardown). Do NOT set `_initialized = false` immediately.
- `CodeEditor_Loaded`: if `_unloadCts` is pending, cancel it and skip teardown entirely. Re-subscribe event handlers only.
- Hard teardown (full state reset): only in `Dispose()` and `OnApplyTemplate()` when replacing the presenter.
- **Soft unload event subscription handling:**
  - `CodeEditor_Unloaded` (soft): unsubscribe `Window.SizeChanged` only (prevents accumulation). Do NOT unsubscribe `Options.PropertyChanged`, `Decorations.VectorChanged`, `Markers.VectorChanged` — these must survive soft cycles.
  - `CodeEditor_Loaded` (soft, after cancel): re-subscribe `Window.SizeChanged` only. Verify existing subscriptions for Options/Decorations/Markers are still active (no duplicates).
  - Hard teardown: unsubscribe ALL handlers (Options, Decorations, Markers, Window.SizeChanged).
- **Subscription count diagnostics (stdout-based, gated, no RPC changes):** `CodeEditor` itself (in the library) emits structured `Console.WriteLine($"DIAG_SUB_COUNTS:{_sizeChangedSubCount},{_optionsSubCount},{_decorationsSubCount},{_markersSubCount}")` on each Loaded/Unloaded event transition, gated behind `MONACO_DIAGNOSTICS=1`. The counters are simple `int` fields incremented on subscribe, decremented on unsubscribe. DesktopCDP tests read process stdout (fixture sets env var) after tab-switch simulation and assert expected counts (e.g., "DIAG_SUB_COUNTS:1,1,1,1" when loaded).
- **Invariants:**
  - `_initialized` is only set to `false` during hard teardown (Dispose/template replacement)
  - `_model` is preserved across soft unload/load cycles
  - Bridge target registration count remains stable (no duplicate registrations)
  - `Window.SizeChanged` subscription count: 0 when unloaded, 1 when loaded
  - `Options.PropertyChanged` subscription count: always exactly 1 while editor exists
  - `Decorations.VectorChanged` subscription count: always exactly 1 while editor exists
  - `Markers.VectorChanged` subscription count: always exactly 1 while editor exists

**Bug 3 fix — No separate queue (LOCKED):**
- All property values live in DependencyProperties and are replayed via `ApplyInitialPropertyValues()` when initialization completes
- No separate queue needed — the DP system already holds the latest values
- Verify that `ApplyInitialPropertyValues()` covers all properties that can be set before init (Text, CodeLanguage, Theme, ReadOnly, HasGlyphMargin, Options, Decorations, Markers)
- Gate all diagnostic `Console.WriteLine` behind `MONACO_DIAGNOSTICS` env var: `if (Environment.GetEnvironmentVariable("MONACO_DIAGNOSTICS") == "1") Console.WriteLine(...)`. This avoids permanent production console noise while enabling Release-testable diagnostics when CI/test sets the env var.
- Change "before initialized" warnings from `Debug.WriteLine` to gated `Console.WriteLine` so they appear in stdout when `MONACO_DIAGNOSTICS=1`
- Emit a distinct `Console.WriteLine("INIT_COMPLETE")` marker (gated) at the exact point where `_initialized` becomes `true`. This is the canonical boundary for warning assertions.
- Also change `editor/ready` and `bridge/ready` handshake log lines from `Debug.WriteLine` to gated `Console.WriteLine`
- `DesktopAppFixture` sets `MONACO_DIAGNOSTICS=1` on the test app process before launch
- After init completes, zero "before initialized" warnings should appear for subsequent calls (testable in Release via process stdout: scan for "before initialized" after the "INIT_COMPLETE" line)

## Key context
- `ParentAccessorDesktop.cs:271-310` — all `[JsonRpcMethod]` handlers with typed record params
- `BridgeContracts.cs:22-45` — the DTO records (SetValueParams, etc.)
- `CodeEditor.cs:243-268` — CodeEditor_Unloaded handler that aggressively tears down
- `CodeEditor.cs:345-421` — SendScriptAsync guards that log "before initialized" warnings
- `DesktopCodeEditorPresenter.cs:619-648` — BridgeHandshakeTarget with bridge/ready handler
- Overlaps with fn-6.2 (initialization race condition) — this task addresses the symptoms needed for testing; fn-6.2 can do a deeper architectural fix later
- StreamJsonRpc named params dispatch: https://github.com/microsoft/vs-streamjsonrpc/issues/48
## Acceptance
- [ ] ALL bridge methods dispatch correctly: zero "arguments do not match" warnings for ALL methods (`parentAccessor/*`, `bridge/ready`, `editor/ready`, `theme/getProperty`, `debug/log`, `keyboard/keyDown`)
- [ ] ALL `[JsonRpcMethod]` handlers across ALL files use individual named params (ParentAccessorDesktop, DesktopCodeEditorPresenter, ThemeListenerDesktop, DebugLoggerDesktop, KeyboardListenerDesktop)
- [ ] ALL unused DTO records removed from `BridgeContracts.cs` (only `LifecycleUpdateParams` retained)
- [ ] `BridgeSerializerContext` updated: remove `[JsonSerializable]` entries for deleted DTOs
- [ ] `bridge-protocol.md` updated: parameter schemas for all 11 methods reflect named-parameter signatures (not DTO-style)
- [ ] Property roundtrip works: setting Text from JS arrives in C#, setting CodeLanguage from C# arrives in JS
- [ ] Tab switching stable for 5 consecutive tab-away/tab-back cycles: `_initialized` stays true, `_model` preserved, `DIAG_SUB_COUNTS` remains `1,1,1,1` after each re-load, no `INIT_COMPLETE` reappears after the initial one
- [ ] Soft unload preserves Options/Decorations/Markers subscriptions; only Window.SizeChanged is toggled. Verified via: `CodeEditor` (library) emits gated `DIAG_SUB_COUNTS:{N},{N},{N},{N}` on Loaded/Unloaded transitions (gated behind `MONACO_DIAGNOSTICS=1`). Tests use fixture's cursor-based log API to parse stdout and assert counts (1,1,1,1 when loaded)
- [ ] Editor preserves its state (text content, language, theme) across tab switches
- [ ] All library diagnostic `Console.WriteLine` calls gated behind `MONACO_DIAGNOSTICS=1` env var (no production console noise)
- [ ] `DesktopAppFixture` sets `MONACO_DIAGNOSTICS=1` on the test app process environment before launch
- [ ] Fixture includes cursor-based log query API: `GetLogCursor()`, `WaitForLogLineAfterAsync(cursor, pattern, timeout)`, `GetLinesAfter(cursor)` (added in this task, used by tasks 8 and 9)
- [ ] Zero "before initialized" warnings after Monaco init completes. Library emits gated `INIT_COMPLETE` at the exact point `_initialized` becomes true. Test uses cursor API to find `INIT_COMPLETE`, then asserts no "before initialized" in lines after it
- [ ] `ApplyInitialPropertyValues()` covers all DP-backed properties (Text, CodeLanguage, Theme, ReadOnly, HasGlyphMargin, Options, Decorations, Markers)
- [ ] Existing unit tests pass: `JsonRpcTargetDispatchTests`, `JsonRpcWireCompatibilityTests`, `BridgeEncodingTests` (updated for new signatures)
- [ ] Desktop test app launches, shows text content, applies theme, and displays syntax highlighting
- [ ] App exit code is 0 (not 0xffffffff) on clean shutdown
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
