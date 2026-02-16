# fn-14-uno-hybridwebview-control-library.3 JS bridge script and async task manager

## Description
Create the JavaScript bridge script (`HybridWebView.js`) and the C# `HybridWebViewTaskManager` for tracking async JS invocations. The bridge uses postMessage protocol (not MAUI's fetch-based approach) to work on both WebView2 and iframe.

**Size:** M
**Files:** `HybridWebViewComponent/ts-hybridwebview/HybridWebView.ts`, `HybridWebViewComponent/ts-hybridwebview/tsconfig.json`, `HybridWebViewComponent/ts-hybridwebview/esbuild.config.mjs`, `HybridWebViewComponent/HybridWebView/HybridWebViewTaskManager.cs`, `HybridWebViewComponent/Serialization/HybridWebViewJsonContext.cs`

## Approach

- **JS Bridge** (TypeScript, compiled to JS):
  - `window.HybridWebView` namespace with:
    - `SendRawMessage(message: string)` — posts to parent via postMessage
    - `InvokeDotNet(methodName: string, ...args: any[])` — posts structured message, returns Promise via task tracking
    - Internal: `__InvokeJavaScript(taskId: string, methodName: string, args: string[])` — called by C# via eval/postMessage
    - Internal: `__CompleteTask(taskId: string, result: string)` — resolves pending JS promise
  - Message format: `{ type: "raw"|"invokeDotNet"|"invokeJSResponse"|"invokeJS", ... }` over postMessage
  - Listen for messages from parent (C#→JS direction) via `window.addEventListener("message", ...)`
  - Auto-initialize on DOMContentLoaded
  - Adapt from MAUI's `HybridWebView.js` but replace fetch with postMessage

- **esbuild pipeline**: Model after `MonacoEditorComponent/ts-helpermethods/esbuild.config.mjs`
  - Bundle to single JS file in `HybridWebViewComponent/WasmScripts/` (for WASM) and `HybridWebViewComponent/HybridWebView/` (for Desktop content)

- **HybridWebViewTaskManager** (C#):
  - Adapt from MAUI's `HybridWebViewTaskManager.cs`
  - `ConcurrentDictionary<string, TaskCompletionSource<string?>>` for tracking in-flight JS invocations
  - `CreateTask()` returns task ID + awaitable Task
  - `CompleteTask(taskId, result)` resolves the TCS
  - Timeout/cancellation support via CancellationToken

- **STJ context**: `HybridWebViewJsonContext` with `[JsonSerializable]` for bridge message types
  - Follow pattern at `MonacoEditorComponent/Serialization/MonacoJsonContext.cs`

## Key context

- MAUI uses fetch POST to `__hwvInvokeDotNet` URL which is intercepted by `WebResourceRequested` — this doesn't work on WASM (no server). postMessage works everywhere.
- MonacoEditorComponent's existing esbuild pipeline is in `ts-helpermethods/esbuild.config.mjs`
- The JS bridge must be embedded as a resource for both platforms
## Acceptance
- [ ] `HybridWebView.ts` compiles to bundled JS via esbuild
- [ ] JS bridge exposes `window.HybridWebView.SendRawMessage()` and `InvokeDotNet()`
- [ ] postMessage protocol handles: raw messages, JS→C# invocations, C#→JS invocations, response routing
- [ ] `HybridWebViewTaskManager` tracks async invocations with `ConcurrentDictionary<string, TaskCompletionSource>`
- [ ] Task manager supports timeout/cancellation via CancellationToken
- [ ] `HybridWebViewJsonContext` with `[JsonSerializable]` for all bridge message types (no reflection)
- [ ] Bundled JS output included as embedded resource in csproj
- [ ] Project builds successfully
## Done summary
- Task completed
## Evidence
- Commits:
- Tests:
- PRs: