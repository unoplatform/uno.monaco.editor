# fn-5-comprehensive-documentation-overhaul.3 Create architecture design docs with Mermaid diagrams

## Description
Create architecture and design documentation for the uno.monaco.editor control with comprehensive Mermaid diagrams. The control has a complex dual-platform interop model that is currently undocumented at the system level.

**Size:** M
**Files:** `docs/architecture.md`, update references in `MonacoEditorComponent/DesktopContent/bridge-protocol.md`

## Approach

Document these architectural aspects with Mermaid diagrams:

### 1. System Overview (C4 Component Diagram or Flowchart)
- C# Application layer → CodeEditor control → Presenter abstraction → Platform-specific presenter → Monaco Editor (JS)
- Show `RenderingBackend` enum determining path selection

### 2. Dual-Platform Interop (Sequence Diagrams)
Two sequence diagrams showing the same operation (e.g., `SetLanguageAsync`) on both paths:

**WASM path:**
```
C# App → CodeEditor → InvokeScriptAsync → WebViewExtensions → NativeMethods.InvokeJS (JSImport) → globalThis eval → Monaco API
JS → C# via JSExport (ParentAccessor.wasm.cs)
```

**Desktop path:**
```
C# App → CodeEditor → InvokeScriptAsync → DesktopCodeEditorPresenter → CoreWebView2.ExecuteScriptAsync → Monaco API
JS → C# via JSON-RPC (vscode-jsonrpc ↔ StreamJsonRpc) → WebView2 postMessage → WebView2JsonRpcMessageHandler → RPC targets
```

### 3. Lifecycle State Machine (stateDiagram-v2)
- `EditorLifecycleState`: Unloaded → Loading → Loaded
- Events: `EditorLoading` (fires once per cycle), `EditorLoaded` (fires once per cycle)
- Desktop initialization handshake: `bridge/ready` notification → `editor/ready` notification

### 4. Presenter Pattern (Class Diagram)
- `ICodeEditorPresenter` interface
- `WasmCodeEditorPresenter` (BrowserHtmlElement wrapper)
- `DesktopCodeEditorPresenter` (WebView2 wrapper)
- Bridge helpers: `IParentAccessor`, `IThemeListener`, `IKeyboardListener`, `IDebugLogger` with WASM/Desktop implementations
- `BridgeFactory` (WASM) vs `CreateBridgeTargets` (Desktop)

### 5. Serialization Layer (Flowchart)
- `MonacoJsonContext` (STJ source-generated, ~40 registered types)
- `BridgeSerializerContext` (bridge DTOs, AOT-safe)
- `WebViewExtensions.InvokeScriptAsync<T>` — constructs JS eval strings, deserializes results
- `Relaxed` variant with `UnsafeRelaxedJsonEscaping`

## Key context

- Existing `bridge-protocol.md` (190 lines) documents JSON-RPC wire protocol — reference it, don't duplicate
- Platform helper interfaces have parallel implementations: `ParentAccessor` (JSExport) / `ParentAccessorDesktop` (JsonRpc), `ThemeListener` / `ThemeListenerDesktop`, etc.
- `BridgeFactory.cs` handles WASM helper creation; desktop creates in `DesktopCodeEditorPresenter.CreateBridgeTargets`
- Use `autonumber` on sequence diagrams for easy reference
- TypeScript entry point: `ts-helpermethods/index.ts` — IIFE bundle via esbuild, assigns ~40 functions to `globalThis`
## Acceptance
- [ ] `docs/architecture.md` exists with all 5 sections documented
- [ ] System overview diagram shows component layers (C# → Presenter → Platform → Monaco JS)
- [ ] Two sequence diagrams: WASM interop flow and Desktop interop flow (both with `autonumber`)
- [ ] Lifecycle state machine diagram (`stateDiagram-v2`) shows Unloaded → Loading → Loaded with events
- [ ] Class diagram shows `ICodeEditorPresenter` hierarchy and bridge helper interfaces
- [ ] Serialization flowchart shows `MonacoJsonContext` → type registration → AOT path
- [ ] All Mermaid diagrams render correctly in GitHub markdown preview
- [ ] Cross-references `bridge-protocol.md` for wire protocol details (no duplication)
- [ ] Documents platform-asymmetric APIs (which methods throw `PlatformNotSupportedException` on desktop)
- [ ] Documents the TypeScript bundle structure (`ts-helpermethods/index.ts` → `globalThis` assignments)
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
