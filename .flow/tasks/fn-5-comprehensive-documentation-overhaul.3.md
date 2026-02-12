## Description
Create architecture and design documentation for the uno.monaco.editor control with comprehensive Mermaid diagrams. The control has a complex dual-platform interop model that is currently undocumented at the system level.

**Size:** M
**Files:** `docs/architecture.md`

## Approach

Document these architectural aspects with Mermaid diagrams:

### 1. System Overview (Flowchart)
C# Application layer → CodeEditor control → Presenter abstraction → Platform-specific presenter → Monaco Editor (JS). Show `RenderingBackend` enum determining path selection.

### 2. Dual-Platform Interop (Sequence Diagrams)
Two sequence diagrams showing the same operation on both paths:

**WASM path**: C# App → CodeEditor → InvokeScriptAsync → WebViewExtensions → NativeMethods.InvokeJS (JSImport) → globalThis eval → Monaco API. JS → C# via JSExport (ParentAccessor.wasm.cs).

**Desktop path**: C# App → CodeEditor → InvokeScriptAsync → DesktopCodeEditorPresenter → CoreWebView2.ExecuteScriptAsync → Monaco API. JS → C# via JSON-RPC (vscode-jsonrpc ↔ StreamJsonRpc) → WebView2 postMessage → WebView2JsonRpcMessageHandler → RPC targets.

### 3. Lifecycle State Machine (stateDiagram-v2)
`EditorLifecycleState`: Unloaded → Loading → Loaded. Events: `EditorLoading` (once per cycle), `EditorLoaded` (once per cycle). Desktop initialization: `bridge/ready` → `editor/ready`.

### 4. Presenter Pattern (Class Diagram)
`ICodeEditorPresenter` interface, `WasmCodeEditorPresenter`, `DesktopCodeEditorPresenter`. Bridge helpers: `IParentAccessor`, `IThemeListener`, `IKeyboardListener`, `IDebugLogger` with WASM/Desktop implementations. `BridgeFactory` (WASM) vs `CreateBridgeTargets` (Desktop).

### 5. Serialization Layer (Flowchart)
`MonacoJsonContext` (STJ source-gen, ~40 types), `BridgeSerializerContext` (bridge DTOs), `WebViewExtensions.InvokeScriptAsync<T>`, `Relaxed` variant.

## Key context
- Existing `bridge-protocol.md` (190 lines) documents JSON-RPC wire protocol — reference it, don't duplicate
- Platform helpers have parallel implementations: `ParentAccessor`/`ParentAccessorDesktop`, `ThemeListener`/`ThemeListenerDesktop`, etc.
- TypeScript entry point: `ts-helpermethods/index.ts` — IIFE bundle via esbuild, assigns ~40 functions to `globalThis`
- Use `autonumber` on sequence diagrams for easy reference

## Acceptance
- [ ] `docs/architecture.md` exists with all 5 sections
- [ ] System overview diagram shows component layers
- [ ] Two sequence diagrams: WASM and Desktop interop flows (both with `autonumber`)
- [ ] Lifecycle state machine diagram shows Unloaded → Loading → Loaded with events
- [ ] Class diagram shows `ICodeEditorPresenter` hierarchy and bridge helpers
- [ ] Serialization flowchart shows `MonacoJsonContext` → type registration → AOT path
- [ ] All Mermaid diagrams render correctly in GitHub markdown preview
- [ ] Cross-references `bridge-protocol.md` (no duplication)
- [ ] Documents platform-asymmetric APIs
- [ ] Documents TypeScript bundle structure

## Done summary

## Evidence
