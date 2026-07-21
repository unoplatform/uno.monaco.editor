# Fix Desktop WebView2 Runtime Bugs

## Overview

Seven interrelated desktop-only (net10.0-desktop / Win11) bugs in the Monaco editor control, all stemming from lifecycle management, JSON-RPC bridge timing, and serialization gaps. The root cause of most symptoms is `OnApplyTemplate()` creating a **new** `DesktopCodeEditorPresenter` (and thus a new WebView2 + JSON-RPC bridge) on every template application — including tab switches — even though each CodeEditor instance should own and retain its own presenter for its entire lifetime. Additionally, `DispatcherQueue.EnqueueAsync` cascading during init may independently contribute to visible flickering by saturating the UI thread with round-trip property requests.

### Symptoms reported by user
1. **Flickering/focus loss** on tab open — editor area flashes black then white
2. **Text not loaded** — auto-loaded content from test app doesn't appear
3. **Tab switching flicker** — same flicker on switching tabs
4. **NullRef on SetSelectedText** — crash when pressing "Set Selected Text" with no selection
5. **JSON-RPC timeout** — `parentAccessor/getJsonValue` times out after 10s during theme init
6. **Editor_Unloaded cycling** — 3x Unloaded/reinit cycles in debug log
7. **ElementTheme serialization** — `NotSupportedException` for `ElementTheme` type not in source-gen context

### Instance model
Each CodeEditor instance owns its own DesktopCodeEditorPresenter (and therefore its own WebView2 + JSON-RPC bridge). Multiple editors on the same page or in different tabs are fully independent. There is NO presenter sharing across instances. The bug is that a single CodeEditor destroys and recreates its OWN presenter every time it re-enters the visual tree (tab switch, layout pass), as shown in the debug log: 3 different presenter IDs (0366FF35, 02DFBF24, 00AFB5B5) for the same editor.

### Root causes
- **Bugs 1/3/6 (flickering, cycling)**: `OnApplyTemplate()` at `CodeEditor.cs:358-432` unconditionally creates a new `DesktopCodeEditorPresenter` (line 409), even when the CodeEditor already has a healthy one. Tab switches trigger re-templating, destroying the existing presenter and bootstrapping from scratch — causing 3x init cycles, black-then-white flash, and focus ping-pong. Additionally, the 100ms deferred teardown CTS creates a race: if the Unloaded→Loaded cycle takes >100ms, hard teardown executes during re-init, leaving the presenter in a half-initialized state (bridge objects torn down but `_view` still set).
- **Bug 1 (white flash, additional factor)**: `DispatcherQueue.EnqueueAsync` cascading during `initializeMonacoEditor` — JS sends multiple `getJsonValue` requests (RequestedTheme, etc.) that each round-trip through UI thread dispatch. Cumulative latency may exceed one frame (16ms), causing visible white flash independent of presenter re-creation.
- **Bug 2 (text not loaded)**: `TextProperty` change handler gates on `IsEditorLoaded` — if text is set during `Loading` phase (before editor ready), push to Monaco is skipped. The cycling from Bug 1 can interrupt `ApplyInitialPropertyValues`.
- **Bug 4 (NullRef)**: `updateSelectedContent.ts:7` uses non-null assertion `getSelection()!`. When no text is selected, the function may receive a null/collapsed selection causing downstream NRE.
- **Bug 5 (JSON-RPC timeout)**: UI-thread deadlock — `InvokeScriptAsync` awaits script evaluation while JS sends JSON-RPC request back to C# that needs UI thread dispatch via `DispatcherQueue.EnqueueAsync`.
- **Bug 7 (serialization)**: `ElementTheme` (WinUI framework enum) is not registered in `MonacoJsonContext`. `SerializePropertyValue` at `ParentAccessorDesktop.cs:388-401` uses `MonacoJsonContext.Relaxed.Options`, catches `NotSupportedException`, and falls back to reflection.

### Architectural investigation: BeginInit/EndInit
The current per-handler `IsEditorLoaded` guards in DP change handlers are fragile and don't cover re-init scenarios. Investigate whether a BeginInit/EndInit deferral pattern (similar to `ISupportInitialize` in WinForms/WPF) would help prevent unnecessary JS roundtrips during initialization. This would defer JS-push side-effects while values/bindings continue working normally, then batch-apply via `ApplyInitialPropertyValues()`. **This is a defense-in-depth investigation, not necessarily the root cause fix** — the primary fix is stopping the presenter from being recreated.

## Scope

- Desktop head (net10.0-desktop) only — WASM path is not affected
- Library code (`MonacoEditorComponent/`) and test app (`MonacoEditorTestApp/`)
- TypeScript helpers (`ts-helpermethods/`)

## Approach

### Task 1: Fix presenter lifecycle — stop recreating per-instance presenters (Bugs 1/3/6)

**Primary fix:** Each CodeEditor must retain its own DesktopCodeEditorPresenter across unload/load cycles. `OnApplyTemplate()` should detect that this editor already has a healthy presenter and skip the destroy/recreate path. Investigate whether the template's ContentPresenter even needs to be touched — if `viewHost.Content` already references the correct presenter, `OnApplyTemplate` can be a no-op for the presenter path.

**Deferred teardown race fix:** Hard teardown (`DeferredTeardownAsync`) must verify the control is still unloaded before nulling state. Soft-reload detection in `CodeEditor_Loaded` should check `_lifecycleState` rather than relying solely on `_unloadCts != null`.

**BeginInit/EndInit investigation:** As a secondary concern, investigate whether a deferral mechanism for DP change handlers would help avoid unnecessary JS roundtrips during the initialization window. This may or may not be needed once the primary lifecycle fix is in place.

**User requirement:** Multiple instances on the same page or different tabs are fully independent. Tab switching must be instantaneous and flicker-free. Loading/unloading should not happen unnecessarily.

### Task 2: Fix JSON-RPC theme init deadlock + text loading + serialization (Bugs 2/5/7)
Break the UI-thread deadlock using a continuation pattern for `createMonacoEditor` invocation: `_ = _view.InvokeScriptAsync(...).ContinueWith(...)` with error propagation via `InternalException` event. The JS side signals completion via `callAction("Loaded")` JSON-RPC notification — add a timeout fallback (30s) to detect if `CodeEditorLoaded` never fires. Fix text loading — Task 1's lifecycle fix should resolve this (no more cycling), but verify empirically.

**ElementTheme serialization:** Add `[JsonSerializable(typeof(Microsoft.UI.Xaml.ElementTheme))]` to `MonacoJsonContext` (primary fix, since `SerializePropertyValue` uses `MonacoJsonContext.Relaxed.Options`). Add to `BridgeSerializerContext` as well if bridge envelope serialization also encounters this type.

**EnqueueAsync investigation:** Add diagnostic logging around `getJsonValueAsync` calls in `asyncCallbackHelpers.ts` to measure per-call and cumulative latency. If cumulative latency exceeds 16ms (one frame), consider batching property requests into a single `getInitialState` RPC call in a follow-up.

### Task 3: Fix null selection guard (Bug 4) + test app safety
**Primary fix:** Add null/collapsed selection guard in `updateSelectedContent.ts` — if `getSelection()` returns null or a collapsed range (start === end), return early without calling `pushEditOperations` (no-op). This matches the "Set Selected Text" button label: if nothing is selected, nothing happens.

**Secondary fix:** Add defensive error handling in test app to demonstrate correct API usage: `IsEditorLoaded` check in `ButtonSetSelectedText_Click`, try-catch in `Editor_Loading`.

**Parallelization note:** Task 2 and Task 3 may be executed in parallel after Task 1 stabilizes the lifecycle, as they modify disjoint file surfaces (bridge/serialization vs. TypeScript/test-app).

## Risks & Dependencies

- **fn-6 overlap**: fn-6 (Bug Fixes, Disposal, Resize, and Test Coverage) touches many of the same files. Tasks in this epic should coordinate to avoid merge conflicts. Epic-scout recommends either merging or declaring dependency.
- **fn-12 overlap**: fn-12 (Promote Editor Options to DPs) involves `ElementTheme` handling. Serialization fix here should not conflict.
- **WebView2 runtime version**: The deadlock behavior may vary across WebView2 Evergreen runtime versions. Fix should be robust against version changes.
- **Uno Platform lifecycle quirks**: Uno fires Loading/Loaded/Unloaded events in different order than WinUI on some platforms (unoplatform/uno#2895, #3519). Desktop head uses native WinUI lifecycle, so this should not apply, but worth noting.
- **Fire-and-forget error handling**: The continuation pattern preserves error propagation, but must be verified against WebView2 IPC failure modes.
- **BeginInit/EndInit scope**: If implemented, the deferral mechanism must be carefully scoped — only JS-push side-effects should be deferred, not DP value storage or `NotifyPropertyChanged` calls.

## Quick commands
```bash
# Build library + test app for desktop
dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop

# Run test app to verify fixes
dotnet run --project MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop

# Build TS helpers (if modified)
cd MonacoEditorComponent/ts-helpermethods && npm run build && cd ../..

# Run unit tests
dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj
```

## Acceptance

- [ ] Each CodeEditor instance retains its own presenter — no destroy/recreate on tab switch
- [ ] Tab switching is instantaneous — no visible delay, flash, or flicker
- [ ] Multiple editor instances on the same page or different tabs work correctly and independently
- [ ] `OnApplyTemplate()` is a no-op (or minimal) for the presenter path when the existing presenter is healthy
- [ ] Editor_Unloaded does not fire repeatedly (max 1x on actual close)
- [ ] Deferred teardown race resolved: hard teardown checks `IsLoaded` before nulling state
- [ ] Test app auto-loaded text (Content.txt) appears in editor on first load
- [ ] "Set Selected Text" with no selection does not throw NullReferenceException (no-op behavior)
- [ ] JSON-RPC `parentAccessor/getJsonValue` completes without 10s timeout
- [ ] Fire-and-forget `createMonacoEditor` preserves error handling via ContinueWith
- [ ] No `System.NotSupportedException` for `ElementTheme` in debug output
- [ ] `ElementTheme` registered in `MonacoJsonContext` (primary) — no reflection fallback
- [ ] Existing WASM functionality is not regressed (CI green)
- [ ] Desktop CDP tests continue to pass on CI
- [ ] BeginInit/EndInit investigation documented: findings on whether it helps, and if implemented, verification that it defers only JS-push side-effects

## References

- `CodeEditor.cs:358-432` — OnApplyTemplate lifecycle
- `CodeEditor.Events.cs:164-191` — WebView_NavigationCompleted, InvokeScriptAsync deadlock point
- `CodeEditor.Events.cs:373-412` — CodeEditorLoaded, ApplyInitialPropertyValues
- `CodeEditor.Events.cs:419-457` — ApplyInitialPropertyValues (batch property push)
- `CodeEditor.Properties.cs:45-81` — Text/SelectedText DP change handlers with IsEditorLoaded guards
- `DesktopCodeEditorPresenter.cs:560-605` — CreateBridgeTargets, SetupJsonRpc
- `ParentAccessorDesktop.cs:328-401` — getJsonValue handler, SerializePropertyValue
- `updateSelectedContent.ts:7` — non-null assertion on getSelection()
- `asyncCallbackHelpers.ts:168-186` — theme init with getJsonValueAsync
- `jsonRpcBridge.ts:192-238` — INIT_REQUEST_TIMEOUT_MS, sendRequestWithTimeout
- `.flow/memory/pitfalls.md` — templated control handler lifecycle, IsLoaded guards, deferred teardown CTS cleanup
- `ISupportInitialize` (WinForms/WPF) — prior art for init deferral pattern
- WebView2 threading model: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/threading-model
- WebView2 performance best practices: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance

## Known Gaps (from gap analysis)

### Must answer during implementation
1. **OnApplyTemplate no-op path:** When a CodeEditor's presenter is still healthy, can `OnApplyTemplate` skip the presenter path entirely? Or does the template's `ContentPresenter` need to be re-assigned? Depends on whether WinUI preserves the Content reference across unload/load cycles.
2. **Does `ExecuteScriptAsync("void ...")` block the UI thread?** The deadlock fix depends on this. Use continuation pattern regardless — it's safe in both cases.
3. **EnqueueAsync latency impact:** Measure during Task 2 implementation. If cumulative round-trip exceeds one frame, batch requests in follow-up.
4. **BeginInit/EndInit value:** After the primary lifecycle fix, determine whether the DP handler deferral mechanism is still needed or if the existing `IsEditorLoaded` guards are sufficient.

### Known CI limitation
Desktop CDP tests run only on Windows CI (`windows-latest`). These bugs are desktop-only and cannot be fully validated on Ubuntu or macOS ARM runners.
