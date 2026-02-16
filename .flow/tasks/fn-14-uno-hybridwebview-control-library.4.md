# fn-14-uno-hybridwebview-control-library.4 Desktop WebView2 presenter

## Description
Implement the Desktop WebView2 presenter that serves local content and bridges C#↔JS communication via WebView2's postMessage channel.

**Size:** M
**Files:** `HybridWebViewComponent/HybridWebView/DesktopHybridWebViewPresenter.cs`

## Approach

- Implement `IHybridWebViewPresenter` for Desktop (WebView2)
- Follow patterns from `MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs`:
  - `EnsureCoreWebView2Async()` initialization
  - Readiness gate before interop calls
  - Visibility management (hide until content loaded to prevent white flash)
  - Deferred teardown pattern for lifecycle safety

- **Content serving**:
  - Use `SetVirtualHostNameToFolderMapping()` to map `HybridRoot` directory to a virtual hostname (e.g., `hybridwebview.localhost`)
  - Navigate to `https://hybridwebview.localhost/{DefaultFile}`
  - Inject `HybridWebView.js` bridge script into the page (via `AddScriptToExecuteOnDocumentCreatedAsync` or `<script>` tag)
  - Adapted from MAUI's `HybridWebViewHandler.Windows.cs` pattern

- **Message routing**:
  - `CoreWebView2.WebMessageReceived` → parse postMessage → dispatch to `RawMessageReceived` or `RegisterDotNetMethod` handlers
  - C#→JS: `CoreWebView2.PostWebMessageAsString()` for raw messages, `ExecuteScriptAsync()` for `InvokeJavaScriptAsync`
  - Response routing: use `HybridWebViewTaskManager` for async invoke tracking

- **Lifecycle**:
  - `InitializeAsync`: create WebView2, configure environment, load content
  - `DisposeAsync`: teardown WebView2, cancel in-flight tasks
  - Handle WebView2 process crash gracefully

## Key context

- `DesktopCodeEditorPresenter.cs` uses `WebView2JsonRpcMessageHandler` + StreamJsonRpc — HybridWebView does NOT use StreamJsonRpc, uses direct postMessage instead
- Must call `EnsureCoreWebView2Async()` before any interop — see memory pitfall about init ordering
- `CoreWebView2.PostWebMessageAsJson()` wraps strings in extra quotes on non-Windows — use `PostWebMessageAsString()` + `TryGetWebMessageAsString()` instead (see `.flow/memory/pitfalls.md`)
- Virtual host mapping requires `CoreWebView2.Settings.IsWebMessageEnabled = true`
## Acceptance
- [ ] `DesktopHybridWebViewPresenter` implements `IHybridWebViewPresenter`
- [ ] WebView2 initializes with `EnsureCoreWebView2Async()` and readiness gate
- [ ] Content served via `SetVirtualHostNameToFolderMapping` from `HybridRoot` directory
- [ ] `HybridWebView.js` bridge injected into pages
- [ ] C#→JS: `EvaluateJavaScriptAsync` and `InvokeJavaScriptAsync` work via `ExecuteScriptAsync`/postMessage
- [ ] JS→C#: `WebMessageReceived` dispatches to registered .NET methods
- [ ] Raw messages: `SendRawMessage` delivers via `PostWebMessageAsString`
- [ ] Visibility managed (hidden until content loads) to prevent white flash
- [ ] Lifecycle: proper init, teardown, and in-flight task cancellation
- [ ] Project builds successfully
## Done summary
- Task completed
## Evidence
- Commits:
- Tests:
- PRs: