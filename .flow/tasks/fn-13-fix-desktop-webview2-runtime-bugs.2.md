# fn-13-fix-desktop-webview2-runtime-bugs.2 Fix JSON-RPC theme init deadlock, text loading, and ElementTheme serialization

## Description
Fix three related issues in the desktop JSON-RPC bridge and initialization path:

1. **JSON-RPC theme init deadlock (Bug 5):** `WebView_NavigationCompleted` awaits `InvokeScriptAsync("void createMonacoEditor(...)")` on the UI thread. JS `initializeMonacoEditor` sends `parentAccessor/getJsonValue("RequestedTheme")` back to C#, which needs `DispatcherQueue.EnqueueAsync` to read the DP on the UI thread — but the UI thread is blocked waiting for `ExecuteScriptAsync` to complete. The `void` operator trick at `CodeEditor.Events.cs:179` is supposed to prevent this, but `ExecuteScriptAsync` may still block awaiting the WebView2 IPC completion.

2. **Text not loading (Bug 2):** `TextProperty` change handler at `CodeEditor.Properties.cs:49` gates on `IsEditorLoaded`. If text is set during the `Loading` phase (before `CodeEditorLoaded` fires), the push to Monaco is skipped. Task 1's BeginInit/EndInit pattern should subsume this issue — text DP changes during init are deferred, then `EndInit()` calls `ApplyInitialPropertyValues()` which pushes text. Verify this resolves naturally after Task 1; if not, add explicit handling.

3. **ElementTheme serialization (Bug 7):** `ElementTheme` is not registered in `MonacoJsonContext`. `SerializePropertyValue` at `ParentAccessorDesktop.cs:388-401` uses `MonacoJsonContext.Relaxed.Options` for serialization and catches `NotSupportedException`, falling back to reflection. Full exception: `System.NotSupportedException: JsonTypeInfo metadata for type 'Microsoft.UI.Xaml.ElementTheme' was not provided by TypeInfoResolver of type 'Monaco.Serialization.MonacoJsonContext'`.

**Size:** M
**Files:**
- `MonacoEditorComponent/CodeEditor/CodeEditor.Events.cs` (InvokeScriptAsync deadlock fix with continuation pattern)
- `MonacoEditorComponent/Helpers/ParentAccessorDesktop.cs` (getJsonValue thread safety verification)
- `MonacoEditorComponent/Serialization/MonacoJsonContext.cs` (primary: ElementTheme registration)
- `MonacoEditorComponent/Bridge/BridgeContracts.cs` (secondary: ElementTheme registration if used in envelope serialization)
- `MonacoEditorComponent/ts-helpermethods/asyncCallbackHelpers.ts` (diagnostic logging for EnqueueAsync latency, theme init error recovery)
- `MonacoEditorComponent/CodeEditor/CodeEditor.Properties.cs` (text loading verification — may be resolved by Task 1's BeginInit/EndInit)

## Approach

- **Deadlock:** Use a continuation pattern that preserves error handling: `_ = _view.InvokeScriptAsync(...).ContinueWith(t => { if (t.IsFaulted) InternalException?.Invoke(this, t.Exception); }, TaskScheduler.FromCurrentSynchronizationContext())`. The JS side signals completion via `callAction("Loaded")` JSON-RPC notification. Add a timeout fallback (30s) to detect if `CodeEditorLoaded` never fires due to script failure.
- **Text loading:** Task 1's BeginInit/EndInit pattern should resolve this: text DP changes during init are deferred and batch-applied via `ApplyInitialPropertyValues()` in `EndInit()`. Verify empirically. If still broken, ensure `ApplyInitialPropertyValues` is called after `_initialized = true` AND `IsEditorLoaded = true` DP is set.
- **Serialization:** Add `[JsonSerializable(typeof(Microsoft.UI.Xaml.ElementTheme))]` to `MonacoJsonContext` (primary fix, since `SerializePropertyValue` uses `MonacoJsonContext.Relaxed.Options`). Also add to `BridgeSerializerContext` if bridge envelope serialization encounters this type. Note pitfall from `.flow/memory/pitfalls.md` line 77: STJ source generator SYSLIB1031 diagnostics cannot be suppressed via #pragma.
- **EnqueueAsync investigation:** Add diagnostic logging around `getJsonValueAsync` calls in `asyncCallbackHelpers.ts` to measure per-call and cumulative latency during init. If cumulative latency exceeds 16ms (one frame), document findings for a follow-up batching optimization.

## Key context

- The `void` operator pattern at `CodeEditor.Events.cs:172-179` is documented inline: "Use void operator so ExecuteScriptAsync returns immediately without awaiting the async Promise." This works for the JS side but may not affect the C# `ExecuteScriptAsync` completion.
- WebView2 threading model: callbacks and event handlers run serially. See: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/threading-model
- The `INIT_REQUEST_TIMEOUT_MS` at `jsonRpcBridge.ts:192` is 10000ms. If the deadlock is fixed, this timeout should never trigger — but keep it as a safety net.
- `ElementTheme` is a WinUI framework enum (namespace `Microsoft.UI.Xaml`). It's a projected type on desktop. Adding it to STJ source-gen context should work since it's a simple enum.
- The continuation pattern via `ContinueWith` preserves error handling that would be lost with a bare fire-and-forget (`_ = ...`). This is critical for diagnosing WebView2 IPC failures.
- Task 1's BeginInit/EndInit subsumes the text loading issue — the same `ApplyInitialPropertyValues()` call that pushes theme/language/options also pushes text content.

## Acceptance
- [ ] `parentAccessor/getJsonValue("RequestedTheme")` completes without 10s timeout during editor initialization
- [ ] No UI-thread deadlock during `createMonacoEditor` invocation
- [ ] Fire-and-forget `createMonacoEditor` uses `ContinueWith` to preserve error handling via `InternalException` event
- [ ] Timeout fallback (30s) detects and reports if `CodeEditorLoaded` never fires
- [ ] Test app auto-loaded text (Content.txt) appears in editor on first load
- [ ] Text set via `Text` DP during init phase is correctly pushed to Monaco via BeginInit/EndInit batch-apply
- [ ] No `System.NotSupportedException` for `ElementTheme` in debug output
- [ ] `ElementTheme` registered in `MonacoJsonContext` (primary) — no reflection fallback
- [ ] Monaco editor theme matches OS theme (dark/light) on first render — no white flash on dark OS
- [ ] Diagnostic logging measures `getJsonValueAsync` round-trip latency during init
- [ ] Existing WASM functionality not regressed
- [ ] Solution builds clean
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
