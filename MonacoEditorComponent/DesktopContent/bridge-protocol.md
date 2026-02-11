# Bridge Protocol Specification

JSON-RPC 2.0 protocol for communication between C# (desktop host) and JavaScript (Monaco Editor in WebView2).

## Wire Format

Standard JSON-RPC 2.0:
```json
{ "jsonrpc": "2.0", "method": "...", "params": {...}, "id": N }
```

## Implementations

- **C# side**: StreamJsonRpc with `SystemTextJsonFormatter` (AOT-compatible, no Newtonsoft.Json)
- **JS side**: `vscode-jsonrpc` (from `vscode-jsonrpc/browser` entry point)

Both libraries implement the same JSON-RPC 2.0 specification and are wire-compatible by design.

## Transport Binding

### JS to C# (outbound from WebView)

JS sends via platform-specific `postWebViewMessage()`:
- **Windows**: `chrome.webview.postMessage(message)`
- **macOS/Linux**: `webkit.messageHandlers.unoWebView.postMessage(message)`

### C# to JS (inbound to WebView)

C# sends via `ICodeEditorPresenter.PostWebMessage(string json)`, which wraps:
- `CoreWebView2.PostWebMessageAsJson(json)`

This presenter-level abstraction decouples the transport from `CoreWebView2` internals.

### Message Reception

- **JS receives**: On Windows, `chrome.webview.addEventListener('message', handler)` receives host messages. On macOS/Linux (WKWebView/WebKitGTK), `window.addEventListener('message', handler)` is used instead.
- **C# receives**: `ICodeEditorPresenter.MessageReceived` event fires with raw JSON from `WebMessageReceived`

## Initialization Handshake

Initialization occurs in two phases:

### Phase 1: Bridge Ready (bundle load)
When the IIFE bundle executes, the JSON-RPC transport is established and JS sends:
```json
{ "jsonrpc": "2.0", "method": "bridge/ready", "params": { "protocolVersion": 1 } }
```
C# validates the protocol version. If mismatched, logs an error and rejects.
At this point the bridge can receive commands, but no Monaco editor instance exists yet.

### Phase 2: Editor Ready (after createMonacoEditor)
After `createMonacoEditor()` completes (Task 4 wires this), JS sends:
```json
{ "jsonrpc": "2.0", "method": "editor/ready", "params": { "protocolVersion": 1 } }
```
C# can now safely invoke editor methods (getValue, updateOptions, etc.).

## JS to C# Methods

### Notifications (no response expected)

| Method | Params | Description |
|--------|--------|-------------|
| `bridge/ready` | `BridgeReadyParams` | JSON-RPC transport initialized (bundle load) |
| `editor/ready` | `EditorReadyParams` | Monaco editor instance created and ready |
| `parentAccessor/setValue` | `SetValueParams` | Property change from JS |
| `parentAccessor/setValueWithType` | `SetValueWithTypeParams` | Typed property change |
| `parentAccessor/callAction` | `CallActionParams` | Invoke named action |
| `parentAccessor/callActionWithParameters` | `CallActionWithParametersParams` | Invoke action with args |
| `debug/log` | `LogParams` | Debug log message |
| `keyboard/keyDown` | `KeyDownParams` | Keyboard event |

### Requests (response expected)

| Method | Params | Returns | Description |
|--------|--------|---------|-------------|
| `parentAccessor/callEvent` | `CallEventParams` | `string?` | Invoke event handler, return result |
| `parentAccessor/getJsonValue` | `GetJsonValueParams` | `string` | Get property value as JSON |
| `theme/getProperty` | `GetThemePropertyParams` | `string` | Get theme property |

## C# to JS Methods

### Notifications

| Method | Params | Description |
|--------|--------|-------------|
| `editor/lifecycleUpdate` | `{ loading: number, loaded: number }` | Lifecycle event counts for testability |
| `editor/updateOptions` | `{ options: object }` | Push updated editor options to Monaco |

### Requests

| Method | Params | Returns | Description |
|--------|--------|---------|-------------|
| `editor/getValue` | `{}` | `string` | Get current editor text |

Additional C# to JS methods may be added as needed during implementation.
Most C# to JS calls continue to use `InvokeScriptAsync` (eval-style) for WASM compatibility.

## Parameter Schemas

```typescript
// JS to C# parameter types
interface BridgeReadyParams {
    protocolVersion: number;  // Must be 1
}

interface EditorReadyParams {
    protocolVersion: number;  // Must be 1
}

interface SetValueParams {
    name: string;
    value: any;  // JsonElement on C# side
}

interface SetValueWithTypeParams {
    name: string;
    value: any;
    typeName: string;
}

interface CallActionParams {
    name: string;
}

interface CallActionWithParametersParams {
    name: string;
    parameters: any;  // Structured JSON: array, object, or primitive
}

interface CallEventParams {
    name: string;
    parameters: any;
}
// Returns: string | null

interface GetJsonValueParams {
    name: string;
}
// Returns: string

interface GetThemePropertyParams {
    name: string;  // "currentThemeName" | "isHighContrast"
}
// Returns: string

interface LogParams {
    level: string;
    message: string;
}

interface KeyDownParams {
    event: any;  // Key event JSON
}
```

## Request/Response Handling

- Handled by JSON-RPC 2.0 spec (`id` field)
- StreamJsonRpc manages correlation, timeouts, and error propagation automatically
- Default per-request timeout: 5000ms (configurable)

## Cleanup

- `JsonRpc.Dispose()` rejects all pending requests
- Called on presenter disposal/navigation
- JS side: `connection.dispose()` cleans up listeners

## Cancellation

- `$/cancelRequest` supported natively by StreamJsonRpc
- Both sides can cancel in-flight requests

## Security Constraints

> **Note**: These constraints are the contract specification. Enforcement is implemented in Task 5 (`WebView2JsonRpcMessageHandler`). The JS bridge in Task 3 does not enforce these -- it trusts the C# host.

### Message Validation (implemented in Task 5)

- **Requests/notifications** (messages with `method` field): Validate method name against known allowlist; validate required params per method; drop unknown methods with warning log
- **Responses** (messages with `id` + `result`/`error`, no `method`): Validate payload structure (must have `id`); StreamJsonRpc handles correlation (unknown IDs safely ignored)

### Limits (implemented in Task 5)

- Maximum payload size: 10MB per message (all envelope types)

### Future

- Capabilities negotiation in `editor/ready` response
