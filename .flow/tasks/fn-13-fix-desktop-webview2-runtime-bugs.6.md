# fn-13-fix-desktop-webview2-runtime-bugs.6 Fix broken desktop init: threading model, push initial state, eliminate unnecessary dispatches

## Description
After Task 1's presenter lifecycle fix (no more 3x cycling — single presenter `0366FF35`), the desktop editor has **three separate failures**:

1. **Theme not applied**: `RequestedTheme` is Dark but editor renders white — not a timing issue, the theme is never applied.
2. **Text not loaded**: Content.txt set via `Text` DP during `Editor_Loading` never appears in Monaco.
3. **Visual flicker**: jitter on first load and tab switch, ending in wrong (white) state.

### Root cause: broken threading model
The desktop JSON-RPC bridge has a fundamentally broken threading model that causes unnecessary dispatch hops, potential deadlocks, and wasted UI thread contention:

**Problem 1: No `SynchronizationContext` on JsonRpc**
`SetupJsonRpc()` at `DesktopCodeEditorPresenter.cs:588-605` creates `JsonRpc` with no `SynchronizationContext`. This means RPC method handlers run on the **thread pool**. Then every handler manually dispatches back to the UI thread via `_queue.EnqueueAsync()`:
- `GetJsonValueAsync` (`ParentAccessorDesktop.cs:138`)
- `SetValueAsync` (`ParentAccessorDesktop.cs:96`)
- `CallAction` (`ParentAccessor.cs:146`)
- `CallActionWithParameters` (`ParentAccessor.cs:166`)
- `GetChildValue` (`ParentAccessorDesktop.cs:162`)
- `SetValue` (`ParentAccessorDesktop.cs:181`)
- `CallEvent` (`ParentAccessorDesktop.cs:264`)
- Plus `ThemeListenerDesktop` and `ParentAccessor` base class handlers

That's 15+ `_queue.EnqueueAsync` calls across the codebase, each adding a dispatch hop for work that could run directly on the UI thread if `JsonRpc.SynchronizationContext` were set.

**Problem 2: No `HasThreadAccess` check before dispatch**
Even if already on the UI thread, `_queue.EnqueueAsync()` is called unconditionally. `PostWebMessageAsync` already does this check (line 270), but nothing else does. Pattern should be:
```csharp
if (_queue.HasThreadAccess) { /* execute directly */ } else { await _queue.EnqueueAsync(...); }
```

**Problem 3: No `ConfigureAwait(false)` anywhere**
Zero uses of `ConfigureAwait(false)` in the entire component. All awaits resume on the captured context by default, potentially holding the UI thread unnecessarily for non-UI work (serialization, logging, etc.).

**Problem 4: JS calls back to C# for values C# already knows**
The init path has JS making 3 async RPC round-trips to read `RequestedTheme`, `getCurrentThemeName`, and `isHighContrast` — values that C# has available synchronously at the point it calls `createMonacoEditor`.

### Evidence that `CodeEditorLoaded` never fires
Logs show `NavigationCompleted (IsSuccess=True)` but no theme/text application. Most likely the `getJsonValueAsync` RPC calls from JS are deadlocking or timing out (each requires `_queue.EnqueueAsync` to read a DP, then the response goes back via `PostWebMessage` which also dispatches). The cascading dispatches may be starving the UI thread or creating a deadlock cycle.

### Why WASM doesn't have this problem
WASM uses synchronous JSExport — `getJsonValue()` is a direct in-process call on the same thread. No dispatch, no round-trip, no threading issues.

**Size:** L
**Files:**
- `MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs` (set `JsonRpc.SynchronizationContext`, `DefaultBackgroundColor`, focus management)
- `MonacoEditorComponent/Helpers/ParentAccessorDesktop.cs` (remove unnecessary `EnqueueAsync`, add `HasThreadAccess` checks, `ConfigureAwait(false)`)
- `MonacoEditorComponent/Helpers/ParentAccessor.cs` (same: `HasThreadAccess` checks, `ConfigureAwait(false)`)
- `MonacoEditorComponent/Helpers/ThemeListenerDesktop.cs` (same pattern)
- `MonacoEditorComponent/CodeEditor/CodeEditor.Events.cs` (push initial state to `createMonacoEditor`, `ConfigureAwait(false)` where appropriate)
- `MonacoEditorComponent/ts-helpermethods/asyncCallbackHelpers.ts` (accept initial state, skip async RPC for provided values)
- `MonacoEditorComponent/DesktopContent/editor.html` (CSS `prefers-color-scheme` background)

## Approach

### Phase 0: Diagnose the exact failure point
Run with `MONACO_DIAGNOSTICS=1` + WebView2 DevTools console to determine: does `createMonacoEditor` run? Do `getJsonValueAsync` calls timeout? Does `callAction("Loaded")` execute?

Make key init-path messages use `Debug.WriteLine` (always visible) instead of only `DiagnosticLog` (requires env var).

### Phase 1: Fix the threading model (root cause)

**Set `JsonRpc.SynchronizationContext`:**
In `SetupJsonRpc()`, capture and set the UI thread's `SynchronizationContext` on the `JsonRpc` instance:
```
_jsonRpc.SynchronizationContext = SynchronizationContext.Current;
```
(Must be called on the UI thread, which `CreateBridgeTargets` is.)

With this set, all RPC method handlers (`OnGetJsonValue`, `OnCallAction`, `OnSetValue`, etc.) will be invoked directly on the UI thread by StreamJsonRpc. This eliminates the need for `_queue.EnqueueAsync` in every handler.

**Remove redundant `_queue.EnqueueAsync` calls:**
Once handlers run on the UI thread via `SynchronizationContext`:
- `GetJsonValueAsync` can call `GetJsonValue` synchronously (already on UI thread)
- `CallAction` can invoke the action directly (no dispatch needed)
- `SetValue`, `GetChildValue`, etc. — same pattern
- Add `HasThreadAccess` guard as safety net: if already on UI thread, execute directly; otherwise dispatch

**Add `ConfigureAwait(false)` where UI thread is not needed:**
- Serialization/deserialization work
- Logging
- Any continuation that doesn't touch DPs or UI elements
- Keep `ConfigureAwait(true)` (default) only where UI thread access is actually required

### Phase 2: Push initial state from C# to JS
Even with the threading fix, pushing initial state eliminates unnecessary round-trips:

**C# side:** Serialize initial state as JSON parameter to `createMonacoEditor`:
- `requestedTheme` / `themeName` / `isHighContrast`
- `text` — current `Text` DP value
- `language` / `readOnly` / other editor options

**JS side:** When initial state is provided (desktop):
- Pass theme + language + readOnly to `monaco.editor.create()` options
- Set content via `editor.setValue()`
- Call `changeTheme()` synchronously with provided values
- **Skip** the async `getJsonValueAsync` round-trips entirely
- Call `callAction("Loaded")` to signal C#

WASM path unchanged.

### Phase 3: CSS and WebView2 background
- `editor.html`: `prefers-color-scheme` media query on `html`/`body`
- `DefaultBackgroundColor` on WebView2 if Uno supports it
- Theme fallback: `window.matchMedia('(prefers-color-scheme: dark)')` instead of hardcoded `'Light'`

### Phase 4: Focus management
- Trace `GotFocus`/`LostFocus` during init to identify ping-pong source
- Investigate `MoveFocusRequested` to suppress focus steal during init window
- Defer `editor.focus()` until after `ApplyInitialPropertyValues`

## Key context
- `JsonRpc` supports `SynchronizationContext` property — when set, method handlers are posted to that context. See: StreamJsonRpc docs on threading.
- `DispatcherQueue.HasThreadAccess` is the WinUI equivalent of WinForms `InvokeRequired` — returns true when the calling thread is the UI thread.
- `CreateBridgeTargets` is called from `InitialiseWebObjects` which runs on the UI thread → `SynchronizationContext.Current` will be the UI thread context at that point.
- `PostWebMessageAsync` already checks `HasThreadAccess` at `DesktopCodeEditorPresenter.cs:270` — this pattern should be applied to all dispatch calls.
- 15+ `_queue.EnqueueAsync` calls in `ParentAccessorDesktop.cs`, `ParentAccessor.cs`, `ThemeListenerDesktop.cs` that would become direct calls with the `SynchronizationContext` fix.
- `callAction("Loaded")` path: JS `callAction` → JSON-RPC notification → `OnCallAction` (thread pool) → `CallAction` → `_queue.EnqueueAsync(() => action.Invoke())` — two dispatch hops that become zero with `SynchronizationContext`.
- `MonitorInitTimeoutAsync` fires after 30s if lifecycle stays in Loading.

## Acceptance
- [ ] `JsonRpc.SynchronizationContext` set to UI thread context — RPC handlers run on UI thread directly
- [ ] Redundant `_queue.EnqueueAsync` calls removed from RPC handlers (replaced with direct execution or `HasThreadAccess`-guarded dispatch)
- [ ] `ConfigureAwait(false)` added where UI thread is not needed
- [ ] C# pushes initial state (theme, text, language, options) to `createMonacoEditor`
- [ ] `monaco.editor.create()` called with correct theme and content from first frame
- [ ] No async RPC round-trips for initial state
- [ ] Theme correctly applied on dark and light OS themes
- [ ] Text (Content.txt) visible in editor on first load
- [ ] No white flash — editor matches OS theme from first visible frame
- [ ] No focus ping-pong during init
- [ ] Tab switch without visible flicker
- [ ] Theme fallback uses OS theme detection
- [ ] `editor.html` has `prefers-color-scheme` CSS background
- [ ] Key init-path messages visible in Debug output by default
- [ ] `HasThreadAccess` guard pattern used consistently (safety net for any non-UI-thread callers)
- [ ] WASM path not regressed
- [ ] Tests added for new behavior
- [ ] Solution builds clean for both net10.0-desktop and net10.0-browserwasm targets

## Done summary
Fixed desktop init threading model by setting JsonRpc.SynchronizationContext to the UI thread (eliminating 15+ redundant _queue.EnqueueAsync dispatch hops), added HasThreadAccess guards with ConfigureAwait(false) throughout the bridge layer, pushed initial state (theme/text/language/readOnly) from C# to createMonacoEditor to eliminate 3 async RPC round-trips, added prefers-color-scheme CSS to prevent white flash, and deferred editor.focus() to prevent focus ping-pong during init.
## Evidence
- Commits: 4fd012b2b18f94265f7b70f251699567f0a7bacb
- Tests: dotnet test --project MonacoEditorComponent.Tests --filter-not-trait Category=DesktopCDP --filter-not-trait Category=WasmPlaywright (182 passed), dotnet build MonacoEditorComponent.slnx (0 warnings, 0 errors), npm run build (ts-helpermethods)
- PRs: