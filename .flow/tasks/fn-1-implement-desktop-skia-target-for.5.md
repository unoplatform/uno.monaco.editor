## Description

Create the `WebView2JsonRpcMessageHandler`, wire up `JsonRpc` on the desktop presenter, and implement desktop C# bridge classes as JSON-RPC targets. Fix all remaining platform guards and provide desktop-safe LanguageIdFromExtension.

**Size:** L (cross-cutting bridge + platform guards — treat as high-risk M)
**Files:** MonacoEditorComponent/Bridge/WebView2JsonRpcMessageHandler.cs (new), MonacoEditorComponent/Helpers/ParentAccessorDesktop.cs (new), MonacoEditorComponent/Helpers/ThemeListenerDesktop.cs (new), MonacoEditorComponent/Helpers/DebugLoggerDesktop.cs (new), MonacoEditorComponent/Helpers/KeyboardListenerDesktop.cs (new), MonacoEditorComponent/Helpers/KeyboardListener.cs (refactor), MonacoEditorComponent/Extensions/WebViewExtensions.cs, MonacoEditorComponent/Monaco/LanguagesHelper.Additions.cs, MonacoEditorComponent/CodeEditor/CodeEditor.cs, MonacoEditorComponent/CodeEditor/CodeEditor.Events.cs, MonacoEditorComponent/Helpers/ThemeListener.cs, MonacoEditorComponent/Helpers/ParentAccessor.cs

## Approach

### WebView2JsonRpcMessageHandler (~50-100 lines)
Implements `IJsonRpcMessageHandler` from StreamJsonRpc:
- **Writer**: Serializes `JsonRpcMessage` to JSON, sends via `ICodeEditorPresenter.PostWebMessage(json)` (presenter-level abstraction, not direct `CoreWebView2`). Uses `SystemTextJsonFormatter` for AOT-compatible serialization (NOT `JsonMessageFormatter`/Newtonsoft).
- **Security validation** (envelope-type-aware): Before feeding incoming messages to StreamJsonRpc, validate based on message type:
  - **Requests/notifications** (have `method` field): (1) method name in allowlist of known methods, (2) required params present per method, (3) payload size <= 10MB. Drop unknown methods with warning log.
  - **Responses** (have `id` + `result`/`error`, no `method`): (1) payload size <= 10MB, (2) `id` field present (StreamJsonRpc handles correlation — only pending IDs are accepted, unknown IDs are safely ignored by the library).
  - This enforces the security constraints from bridge-protocol.md at the transport layer.
- **Reader**: Receives from `ICodeEditorPresenter.MessageReceived` event, deserializes JSON into `JsonRpcMessage`, feeds into `Channel<JsonRpcMessage>`. The `ChannelReader<JsonRpcMessage>` is returned from `IJsonRpcMessageHandler.Reader`.
- **Lifecycle**: Created after `CoreWebView2InitializationCompleted`. Disposed when presenter disposes (which disposes `JsonRpc`, which rejects all pending requests).

### JsonRpc wiring
- In `DesktopCodeEditorPresenter` (after `EnsureCoreWebView2Async`):
  1. Create `WebView2JsonRpcMessageHandler`
  2. Create `JsonRpc(handler)` instance
  3. Attach bridge targets (see below) via `JsonRpc.AddLocalRpcTarget()`
  4. Call `JsonRpc.StartListening()`
- `JsonRpc` instance exposed to bridge classes (not publicly — internal or via constructor injection)

### Bridge classes as JSON-RPC targets
Desktop bridge classes register their methods on the shared `JsonRpc` instance. StreamJsonRpc routes incoming JSON-RPC messages to the correct method automatically — **no manual message parsing or routing code needed**.

- **ParentAccessorDesktop**: Implements interface extracted in Task 2. Registered as local RPC target. Method signatures use typed param objects matching `bridge-protocol.md` DTO contracts:
  - `[JsonRpcMethod("parentAccessor/setValue")] void OnSetValue(SetValueParams p)` — property update from JS
  - `[JsonRpcMethod("parentAccessor/setValueWithType")] void OnSetValueWithType(SetValueWithTypeParams p)`
  - `[JsonRpcMethod("parentAccessor/callAction")] void OnCallAction(CallActionParams p)`
  - `[JsonRpcMethod("parentAccessor/callActionWithParameters")] void OnCallActionWithParameters(CallActionWithParametersParams p)` — `p.Parameters` (`JsonElement`) converted to `string[]` via deterministic mapping: `Array` → element-wise `GetRawText()`, `String` → single-element array, `Null`/`Undefined` → empty array, any other type (object, number, bool) → single-element `GetRawText()`. Same mapping for `CallEventParams.Parameters`.
  - `[JsonRpcMethod("parentAccessor/callEvent")] Task<string?> OnCallEvent(CallEventParams p)` — request: JS awaits response
  - `[JsonRpcMethod("parentAccessor/getJsonValue")] string OnGetJsonValue(GetJsonValueParams p)` — request: reflection lookup → return JSON
  - Param record types defined in `MonacoEditorComponent/Bridge/BridgeContracts.cs` (shared between all targets). Use `record` types with primary constructors (`.NET 10 style`). All properties `required` where applicable.
  - Uses same `Dictionary<string, ParentAccessorDesktop>` keyed on instance ID for multi-editor support

### AOT-friendly serialization
- **`SystemTextJsonFormatter`**: Configure `JsonRpc` with `SystemTextJsonFormatter` (from StreamJsonRpc package), NOT the default `JsonMessageFormatter` (Newtonsoft). Pass `JsonSerializerOptions` with source-generated `JsonSerializerContext`.
- **`[JsonSerializable]` context**: Create `BridgeSerializerContext` with `[JsonSerializable(typeof(SetValueParams))]`, `[JsonSerializable(typeof(CallEventParams))]`, etc. for all DTO types. This enables compile-time serialization code generation — no reflection at runtime.
- **Naming policy**: `JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase` to match JS property names.
- **No Newtonsoft.Json**: Ensure zero transitive dependency on Newtonsoft. If StreamJsonRpc pulls it in, explicitly exclude or use only `SystemTextJsonFormatter`.

### .NET 10 coding standards
- File-scoped namespaces throughout
- Primary constructors for DTO records: `record SetValueParams(string Name, JsonElement Value);`
- `required` modifier on properties where applicable
- Collection expressions (`[..]`) for array initialization
- Pattern matching for type checks
- `ArgumentNullException.ThrowIfNull()` instead of manual null checks

- **Sanitize/Desanitize guard**: Shared `ParentAccessor.cs` setter calls `Desanitize()`. Desktop values arrive as clean JSON via JSON-RPC — skip. Guard with `OperatingSystem.IsBrowser()`. Add acceptance for quote/newline/tab round-trips. NOTE: Current WASM path has value-flow inconsistencies (`SetValue` computes desanitized value but sets `newValue`). Fix the WASM path value flow here and verify with Task 6 tests.

- **Text property loop prevention**: When the `Text` DP is set from C# (e.g., MVVM binding), it invokes a script to update JS. JS then fires `onDidChangeModelContent` which sends `parentAccessor/setValue` notification back via JSON-RPC. The bridge must detect this echo and suppress it. Implementation approach:
  - Track a `_changePending` flag in `ParentAccessorDesktop`
  - When C# initiates a text change, set the flag before invoking script
  - When the `OnSetValue` JSON-RPC handler receives a `setValue` for the same property while the flag is set, skip the C# property update
  - Clear the flag after the round-trip completes or on timeout
  - This prevents infinite update loops in two-way bound scenarios

- **ThemeListenerDesktop**: Implements extracted interface. Detects OS theme, fires ThemeChanged. Registered as RPC target:
  - `[JsonRpcMethod("theme/getProperty")] string OnGetThemeProperty(GetThemePropertyParams p)` — returns `currentThemeName` or `isHighContrast` value

- **DebugLoggerDesktop**: Registered as RPC target:
  - `[JsonRpcMethod("debug/log")] void OnLog(LogParams p)` — routes to `Debug.WriteLine`

- **KeyboardListenerDesktop**: Registered as RPC target:
  - `[JsonRpcMethod("keyboard/keyDown")] void OnKeyDown(KeyDownParams p)` — routes key event
  - `[JSExport] NativeKeyDown` at `KeyboardListener.cs:70` guarded with `OperatingSystem.IsBrowser()`

- **editor/ready handler**: Registered as RPC target:
  - `[JsonRpcMethod("editor/ready")] void OnEditorReady(EditorReadyParams p)` — validates `p.ProtocolVersion`, signals initialization complete

- **Lifecycle event push** (C#→JS): When `EditorLoading`/`EditorLoaded` fire (via Task 2's lifecycle state machine), emit `JsonRpc.NotifyAsync("editor/lifecycleUpdate", new { loading = N, loaded = N })` with current event counts. This enables Task 8's Playwright tests to verify exactly-once semantics from within the WebView2 DOM.

### Remaining platform fixes
- **LanguageIdFromExtension**: Currently `[JSImport]` — throws on desktop. Provide C#-side mapping dictionary or async JSON-RPC call. Public API change allowed if needed.
- **Window.Current**: Replace at `CodeEditor.cs:186-189` and `ThemeListener.cs:51-53,66-68`.
- **InitialiseWebObjects convergence**: Create correct helper variants based on `OperatingSystem.IsBrowser()`. Desktop path wires up `JsonRpc` targets. WASM path unchanged.

## Acceptance

- [ ] `WebView2JsonRpcMessageHandler` implements `IJsonRpcMessageHandler` with Writer (via `ICodeEditorPresenter.PostWebMessage`) and Reader (Channel from `ICodeEditorPresenter.MessageReceived`)
- [ ] `JsonRpc` instance created and started in DesktopCodeEditorPresenter init
- [ ] Bridge classes registered as local RPC targets via `JsonRpc.AddLocalRpcTarget()`
- [ ] No manual message parsing or type-field routing code — StreamJsonRpc handles all dispatch
- [ ] Security validation in message handler: envelope-type-aware (requests/notifications: method allowlist + required params; responses: id/result/error schema), 10MB payload limit
- [ ] C# method signatures use typed param records matching bridge-protocol.md DTO contracts
- [ ] `editor/ready` notification validates `protocolVersion`
- [ ] ParentAccessorDesktop JSON-RPC methods handle all bridge operations
- [ ] `parentAccessor/getJsonValue` request round-trip works end-to-end on desktop
- [ ] `parentAccessor/callEvent` request round-trip works end-to-end on desktop
- [ ] Sanitize/desanitize guarded by `OperatingSystem.IsBrowser()`
- [ ] Quote/newline/tab round-trip in Text/SelectedText
- [ ] Text property loop prevention implemented (no infinite loops in two-way binding)
- [ ] `JsonRpc.Dispose()` cleans up all pending requests (no manual cleanup code)
- [ ] ThemeListenerDesktop registered as RPC target, fires ThemeChanged
- [ ] DebugLoggerDesktop registered as RPC target, logs messages
- [ ] KeyboardListener [JSExport] guarded, desktop routing via JSON-RPC
- [ ] LanguageIdFromExtension works on desktop
- [ ] Window.Current replaced everywhere
- [ ] InitialiseWebObjects creates correct variants per platform (desktop wires JsonRpc targets)
- [ ] `editor/lifecycleUpdate` notification emitted via `JsonRpc.NotifyAsync` when lifecycle events fire (consumed by Task 8 tests)
- [ ] `SystemTextJsonFormatter` used (not Newtonsoft `JsonMessageFormatter`) — AOT-safe
- [ ] `[JsonSerializable]` source-generated context covers all DTO types
- [ ] `CamelCase` naming policy configured for JS interop
- [ ] No Newtonsoft.Json dependency at runtime
- [ ] .NET 10 coding standards: file-scoped namespaces, primary constructors for records, `required` modifier
- [ ] No WASM regression
