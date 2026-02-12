# Architecture

This document describes the internal architecture of the `Uno.Monaco.Editor` control, covering the component layer model, dual-platform interop paths, editor lifecycle, presenter pattern, and serialization layer.

For the JSON-RPC wire protocol used on desktop, see [bridge-protocol.md](../MonacoEditorComponent/DesktopContent/bridge-protocol.md).

## System Overview

The control wraps the [Monaco Editor](https://microsoft.github.io/monaco-editor/) inside a platform-appropriate web host. A `RenderingBackend` enum (set automatically at construction time via `OperatingSystem.IsBrowser()`) selects which presenter implementation is used.

```mermaid
flowchart TD
    App["C# Application"]
    CE["CodeEditor Control"]
    RB{"RenderingBackend"}
    WASM["WasmCodeEditorPresenter\n(BrowserHtmlElement)"]
    Desktop["DesktopCodeEditorPresenter\n(WebView2)"]
    MonacoJS["Monaco Editor (JS)"]

    App --> CE
    CE --> RB
    RB -- "Wasm" --> WASM
    RB -- "Desktop" --> Desktop
    WASM --> MonacoJS
    Desktop --> MonacoJS
```

Key layers from top to bottom:

| Layer | Responsibility |
|-------|---------------|
| **C# Application** | Consumes `CodeEditor` control, sets properties, subscribes to events |
| **CodeEditor** | Templated `Control`; owns lifecycle, property sync, bridge helper wiring |
| **ICodeEditorPresenter** | Abstraction over the web host; two implementations selected by platform |
| **WasmCodeEditorPresenter** | WASM: wraps `BrowserHtmlElement` (iframe-like DOM element) |
| **DesktopCodeEditorPresenter** | Desktop (Skia): wraps `WebView2` with CoreWebView2 |
| **Monaco Editor** | The JavaScript editor running inside the web host |

## Dual-Platform Interop

The control communicates with Monaco through two fundamentally different interop paths depending on the platform. Both paths provide the same high-level capabilities (property sync, action dispatch, event callbacks) but use different transport mechanisms.

### WASM Interop Path

On WebAssembly, .NET and JavaScript share the same browser process. Communication uses `JSImport`/`JSExport` attributes (part of the .NET WASM interop layer) for direct interop calls (synchronous or asynchronous depending on the API).

```mermaid
sequenceDiagram
    autonumber
    participant App as C# Application
    participant CE as CodeEditor
    participant Ext as WebViewExtensions
    participant WASM as WasmCodeEditorPresenter
    participant NM as NativeMethods (JSImport)
    participant GS as globalThis (JS)
    participant Monaco as Monaco API

    App->>CE: SetPositionAsync(position)
    CE->>Ext: InvokeScriptAsync("setPosition", args)
    Ext->>Ext: Serialize args via MonacoJsonContext.Relaxed
    Ext->>WASM: InvokeScriptAsync(script)
    WASM->>NM: NativeMethods.InvokeJS(elementId, script)
    NM->>GS: globalThis eval
    GS->>Monaco: editor.setPosition(...)
    Monaco-->>GS: result
    GS-->>NM: JSON string
    NM-->>WASM: string
    WASM-->>Ext: Task<string>
    Ext->>Ext: Deserialize via MonacoJsonContext.Default
    Ext-->>CE: T? result
```

**JS to C# (WASM):** Monaco calls back into .NET through `JSExport`-decorated static methods on `ParentAccessor.wasm.cs`. A `ConditionalWeakTable` maps the JS-passed managed owner reference to the correct `ParentAccessor` instance.

```mermaid
sequenceDiagram
    autonumber
    participant Monaco as Monaco API (JS)
    participant GS as globalThis helpers
    participant JSE as JSExport boundary
    participant PA as ParentAccessor.wasm.cs
    participant CE as CodeEditor

    Monaco->>GS: callParentEventAsync(element, name, args)
    GS->>JSE: ParentAccessor.ManagedCallEvent(owner, name, params)
    JSE->>PA: ManagedCallEvent(managedOwner, name, params)
    PA->>PA: Desanitize parameters
    PA->>CE: CallEvent(name, params) via DispatcherQueue
    CE-->>PA: result string
    PA-->>JSE: string?
    JSE-->>GS: return value
```

### Desktop Interop Path

On desktop (Skia), Monaco runs inside a WebView2 process. Communication uses two mechanisms:

- **C# to JS:** `CoreWebView2.ExecuteScriptAsync` (eval-style, same API shape as WASM)
- **JS to C#:** JSON-RPC 2.0 over WebView2 `postMessage`/`WebMessageReceived` transport, powered by `StreamJsonRpc` (C#) and `vscode-jsonrpc` (JS)

```mermaid
sequenceDiagram
    autonumber
    participant App as C# Application
    participant CE as CodeEditor
    participant Ext as WebViewExtensions
    participant DP as DesktopCodeEditorPresenter
    participant CWV as CoreWebView2
    participant Monaco as Monaco API

    App->>CE: SetPositionAsync(position)
    CE->>Ext: InvokeScriptAsync("setPosition", args)
    Ext->>Ext: Serialize args via MonacoJsonContext.Relaxed
    Ext->>DP: InvokeScriptAsync(script)
    DP->>CWV: ExecuteScriptAsync(script)
    CWV->>Monaco: eval in WebView2 process
    Monaco-->>CWV: JSON result
    CWV-->>DP: string
    DP-->>Ext: Task<string>
    Ext->>Ext: Deserialize via MonacoJsonContext.Default
    Ext-->>CE: T? result
```

**JS to C# (Desktop):** Monaco sends JSON-RPC notifications/requests through the bridge transport. Messages flow through a validation layer before reaching StreamJsonRpc.

```mermaid
sequenceDiagram
    autonumber
    participant Monaco as Monaco API (JS)
    participant Bridge as jsonRpcBridge.ts
    participant WV as WebView2 postMessage
    participant Handler as WebView2JsonRpcMessageHandler
    participant RPC as StreamJsonRpc
    participant PAD as ParentAccessorDesktop
    participant CE as CodeEditor

    Monaco->>Bridge: connection.sendNotification("parentAccessor/setValue", params)
    Bridge->>WV: postWebViewMessage(json)
    WV->>Handler: MessageReceived event
    Handler->>Handler: Validate: size ≤ 10MB, method allowlist, required params
    Handler->>RPC: Channel<ReadOnlySequence<byte>>
    RPC->>PAD: OnSetValue(SetValueParams) via [JsonRpcMethod]
    PAD->>CE: SetValue(name, value) via DispatcherQueue
```

### Platform API Parity

All public `CodeEditor` APIs work identically on WASM and desktop. The unified `InvokeMethodAsync` on `ICodeEditorPresenter` handles element resolution per-platform, ensuring scripts reference the correct editor element on both transport paths.

| API | WASM | Desktop | Notes |
|-----|------|---------|-------|
| `AddActionAsync` | Supported | Supported | Action callbacks routed through `ParentAccessor` on both platforms |
| `AddCommandAsync` | Supported | Supported | Command callbacks routed through `ParentAccessor` on both platforms |
| `PostWebMessage` | `PlatformNotSupportedException` | Supported | WASM uses JSExport direct calls instead (internal transport, not public API) |
| `ICodeEditorPresenter.MessageReceived` | Never fires | Fires from WebMessageReceived | WASM callbacks bypass message events entirely (internal transport detail) |

## Lifecycle State Machine

The editor follows a three-state lifecycle managed by `EditorLifecycleState`. Transitions are enforced by `TransitionLifecycle()` which fires events exactly once per cycle.

```mermaid
stateDiagram-v2
    [*] --> Unloaded

    Unloaded --> Loading : InitialiseWebObjects succeeds
    Loading --> Loaded : Navigation completes or<br/>CodeEditorLoaded callback
    Loaded --> Unloaded : CodeEditor_Unloaded or<br/>OnApplyTemplate (re-template)

    Unloaded --> Unloaded : TeardownWebObjects (idempotent)

    state Unloaded {
        direction LR
        note right of Unloaded
            IsEditorLoaded = false
            _initialized = false
        end note
    }

    state Loading {
        direction LR
        note right of Loading
            EditorLoading event fires (once)
            Bridge helpers wired
        end note
    }

    state Loaded {
        direction LR
        note right of Loaded
            EditorLoaded event fires (once)
            IsEditorLoaded = true
            _initialized = true
        end note
    }
```

**Initialization sequence:**

1. `OnApplyTemplate` creates the presenter (`WasmCodeEditorPresenter` or `DesktopCodeEditorPresenter`) and attaches navigation event handlers.
2. `WebView_DOMContentLoaded` (or presenter `Loaded`) calls `InitialiseWebObjects()`, which creates bridge helpers and transitions to `Loading`.
3. The presenter's `Launch()` method is called (WASM: `createMonacoEditor` via JSImport; Desktop: `EnsureCoreWebView2Async` + security settings).
4. On navigation completion (`WebView_NavigationCompleted`) or the `"Loaded"` callback from JS, the lifecycle transitions to `Loaded`.

**Desktop initialization handshake (informational):**

On desktop, two JSON-RPC notifications are observed during initialization. These are currently informational signals logged by `BridgeHandshakeTarget` -- they do not drive lifecycle state transitions (which are driven by `WebView_NavigationCompleted` and the `"Loaded"` callback):
- `bridge/ready` (typically arrives at IIFE bundle load, before any editor exists)
- `editor/ready` (typically arrives after `createMonacoEditor()` completes)

See [bridge-protocol.md](../MonacoEditorComponent/DesktopContent/bridge-protocol.md) for the full handshake specification.

## Presenter Pattern

The presenter pattern decouples the `CodeEditor` control from platform-specific web host implementations.

```mermaid
classDiagram
    class ICodeEditorPresenter {
        <<interface>>
        +Source : Uri
        +ElementId : string
        +IsLoaded : bool
        +IsSettingValue : bool
        +ParentCodeEditor : CodeEditor?
        +DispatcherQueue : DispatcherQueue
        +Launch() Task
        +InvokeScriptAsync(script) Task~string~
        +PostWebMessage(json) void
        +TriggerKeyDown(args) bool
        +Focus(state) bool
        +NavigationStarting : event
        +NavigationCompleted : event
        +NewWindowRequested : event
        +MessageReceived : event
        +Loaded : event
    }

    class WasmCodeEditorPresenter {
        -_element : BrowserHtmlElement
        +InvokeScriptAsync() NativeMethods.InvokeJS
        +PostWebMessage() PlatformNotSupportedException
    }

    class DesktopCodeEditorPresenter {
        -_webView : WebView2
        -_jsonRpc : JsonRpc?
        -_messageHandler : WebView2JsonRpcMessageHandler?
        +InvokeScriptAsync() CoreWebView2.ExecuteScriptAsync
        +PostWebMessage() CoreWebView2.PostWebMessageAsJson
        +CreateBridgeTargets(queue) tuple
        +Rpc : JsonRpc? (internal)
    }

    class CodeEditor {
        -_view : ICodeEditorPresenter?
        -_parentAccessor : IParentAccessor?
        -_themeListener : IThemeListener?
        -_keyboardListener : IKeyboardListener?
        -_debugLogger : IDebugLogger?
        +RenderingBackend : RenderingBackend
    }

    ICodeEditorPresenter <|.. WasmCodeEditorPresenter
    ICodeEditorPresenter <|.. DesktopCodeEditorPresenter
    CodeEditor o-- ICodeEditorPresenter : _view
```

### Bridge Helpers

Bridge helpers mediate between the JavaScript runtime and the C# control. Each helper has a WASM variant (using `JSExport` static methods with `ConditionalWeakTable` instance lookup) and a desktop variant (using `[JsonRpcMethod]` attributes for StreamJsonRpc dispatch).

```mermaid
classDiagram
    class IParentAccessor {
        <<interface>>
        +RegisterAction(name, action)
        +RegisterActionWithParameters(name, action)
        +RegisterEvent(name, function)
        +RegisterTypeInfo(name, info)
        +GetValue(name) Task~object?~
        +GetJsonValue(name) string
        +SetValue(name, value) Task
        +SetValue(name, value, type) Task
        +CallAction(name) bool
        +CallEvent(name, params) Task~string?~
    }

    class IThemeListener {
        <<interface>>
        +CurrentThemeName : string
        +CurrentTheme : ApplicationTheme
        +IsHighContrast : bool
        +ThemeChanged : event
    }

    class IKeyboardListener {
        <<interface>>
        +KeyDown(keycode, ctrl, shift, alt, meta) bool
    }

    class IDebugLogger {
        <<interface>>
        +Log(message)
    }

    class ParentAccessor {
        WASM: JSExport static methods
        ConditionalWeakTable lookup
    }

    class ParentAccessorDesktop {
        Desktop: [JsonRpcMethod] targets
        StreamJsonRpc dispatch
    }

    class ThemeListener {
        WASM: JSExport static methods
    }

    class ThemeListenerDesktop {
        Desktop: [JsonRpcMethod] targets
    }

    class KeyboardListener {
        WASM: JSExport static methods
    }

    class KeyboardListenerDesktop {
        Desktop: [JsonRpcMethod] targets
    }

    class DebugLogger {
        WASM: JSExport static methods
    }

    class DebugLoggerDesktop {
        Desktop: [JsonRpcMethod] targets
    }

    IParentAccessor <|.. ParentAccessor
    IParentAccessor <|.. ParentAccessorDesktop
    IThemeListener <|.. ThemeListener
    IThemeListener <|.. ThemeListenerDesktop
    IKeyboardListener <|.. KeyboardListener
    IKeyboardListener <|.. KeyboardListenerDesktop
    IDebugLogger <|.. DebugLogger
    IDebugLogger <|.. DebugLoggerDesktop
```

**Creation paths:**

| Platform | Factory | Registration |
|----------|---------|-------------|
| WASM | `BridgeFactory.Create(presenter, queue)` | Helpers registered in `ConditionalWeakTable` keyed by presenter instance |
| Desktop | `DesktopCodeEditorPresenter.CreateBridgeTargets(queue)` | Helpers registered as `JsonRpc.AddLocalRpcTarget()` for StreamJsonRpc dispatch |

## Serialization Layer

The control uses System.Text.Json (STJ) source generation for AOT-compatible serialization across the interop boundary. Two separate `JsonSerializerContext` classes handle different scopes.

```mermaid
flowchart TD
    subgraph MonacoJsonContext ["MonacoJsonContext (~40 types)"]
        direction TB
        MJC_Desc["STJ source-generated context\nCamelCase naming\nWhenWritingNull ignore"]
        Types["Position, Range, Selection,\nCompletionItem, CodeAction,\nHover, IMarkerData, ..."]
        Arrays["Array variants for each type\n(Position[], Range[], ...)"]
        Relaxed["MonacoJsonContext.Relaxed\nUnsafeRelaxedJsonEscaping\nfor code content"]
        TypeMap["BuildTypeInfoMap()\nConcurrentDictionary<string, JsonTypeInfo>\nFQN + short name keys"]
    end

    subgraph BridgeSerializerContext ["BridgeSerializerContext (bridge DTOs)"]
        direction TB
        BSC_Desc["STJ source-generated context\nCamelCase naming\nDesktop JSON-RPC only"]
        DTOs["BridgeReadyParams, SetValueParams,\nCallActionParams, LogParams,\nLifecycleUpdateParams, ..."]
    end

    subgraph Consumers ["Consumers"]
        direction TB
        WVE["WebViewExtensions\nInvokeScriptAsync<T>"]
        PA["ParentAccessor\nSetValue(name, value, type)"]
        SJR["StreamJsonRpc\nSystemTextJsonFormatter"]
    end

    MonacoJsonContext --> WVE
    MonacoJsonContext --> PA
    BridgeSerializerContext --> SJR

    WVE -- "Serialize: Relaxed (code chars)\nDeserialize: Default" --> MonacoJsonContext
    PA -- "Deserialize via\nTypeInfoMap lookup" --> MonacoJsonContext
    SJR -- "Serialize/Deserialize\nJSON-RPC params" --> BridgeSerializerContext
```

### Serialization paths

| Path | Context | Encoder | Usage |
|------|---------|---------|-------|
| C# to JS (script building) | `MonacoJsonContext.Relaxed` | `UnsafeRelaxedJsonEscaping` | Serializing args for `InvokeScriptAsync` eval strings; avoids escaping `<`, `>`, `&` in code content |
| JS to C# (result parsing) | `MonacoJsonContext.Default` | Default (safe) | Deserializing return values from `InvokeScriptAsync` |
| JS to C# (typed SetValue) | `MonacoJsonContext.Default` via `BuildTypeInfoMap()` | Default (safe) | `ParentAccessor.SetValue(name, json, typeName)` looks up `JsonTypeInfo` by FQN or short name |
| Desktop JSON-RPC | `BridgeSerializerContext.Default` | Default (safe) | `SystemTextJsonFormatter` for StreamJsonRpc serialization of bridge DTOs |

### AOT safety

- `MonacoJsonContext.BuildTypeInfoMap()` builds a `ConcurrentDictionary<string, JsonTypeInfo>` at construction time, keyed by both fully-qualified name (e.g., `"Monaco.Position"`) and short name (e.g., `"Position"`) for backward compatibility with JS callers.
- Runtime `Type.GetType()` is never used for deserialization. All type resolution goes through the pre-built map.
- Consumer code can register additional types via `IParentAccessor.RegisterTypeInfo()`.

## TypeScript Bundle Structure

The JavaScript side is built as a single IIFE bundle (`uno-monaco-helpers.js`) from `MonacoEditorComponent/ts-helpermethods/index.ts` using esbuild.

**Module layout:**

| Module | Responsibility |
|--------|---------------|
| `index.ts` | Entry point; imports Monaco ESM, configures workers, assigns ~40 functions to `globalThis`, auto-inits bridge on desktop |
| `asyncCallbackHelpers.ts` | `createMonacoEditor`, `InvokeJS`, sanitize/desanitize, parent accessor call helpers |
| `otherScriptsToBeOrganized.ts` | `EditorContext`, editor manipulation functions (updateContent, updateOptions, etc.), theme accessor helpers |
| `registerCompletionItemProvider.ts` | Completion provider bridge |
| `registerCodeActionProvider.ts` | Code action provider bridge |
| `registerCodeLensProvider.ts` | Code lens provider bridge |
| `registerColorProvider.ts` | Color provider bridge |
| `updateSelectedContent.ts` | Selection content bridge |
| `bridge/jsonRpcBridge.ts` | `vscode-jsonrpc` connection management (`createBridgeConnection`, `isDesktopHost`, `getConnection`) |

**Desktop auto-init sequence:**

1. Bundle IIFE executes, `isDesktopHost()` returns true
2. `createBridgeConnection()` establishes the `vscode-jsonrpc` connection over `postMessage`
3. `connection.listen()` starts the message loop
4. `connection.sendNotification('bridge/ready', { protocolVersion: 1 })` signals transport readiness
5. After `createMonacoEditor()` completes, `connection.sendNotification('editor/ready', { protocolVersion: 1 })` signals editor readiness

**globalThis assignments:** All public functions are explicitly assigned to `globalThis` (not via esbuild `globalName`) so that both `JSImport("globalThis.*")` (WASM) and `ExecuteScriptAsync` eval (Desktop) can call them.
