# fn-10-fix-reverted-emitter-docs-enable-cdp-ci.6 Enable AddActionAsync/AddCommandAsync on desktop

## Description
`AddActionAsync` and `AddCommandAsync` do not work on desktop due to a leaky platform abstraction. Fix this by creating a single unified method-invocation path on `ICodeEditorPresenter` that encapsulates element resolution, then migrate ALL internal callers to it. After this task, every public `CodeEditor` API works identically on WASM and desktop — developers should never need to check the platform (though `ICodeEditorPresenter` type is still available for introspection). Update all docs that say "WASM only" and add explicit Linux/WSL2 prerequisites.

**Guiding principle:** One API, full parity. If it's on `CodeEditor`, it works everywhere.

**Size:** L
**Files:** `ICodeEditorPresenter.cs`, `WasmCodeEditorPresenter.cs`, `DesktopCodeEditorPresenter.cs`, `WebViewExtensions.cs`, `CodeEditor.cs`, `CodeEditor.Methods.cs`, `CodeEditor.Events.cs`, `docs/getting-started.md`, `docs/cookbook.md`

## Root cause analysis

### Architecture problem: leaky `element` abstraction
`WebViewExtensions.cs:133` builds JS scripts as `method + "(element," + args + ")"`. This hardcodes a dependency on a global `element` variable that:
- **WASM**: Defined per-call inside `InvokeJS` (asyncCallbackHelpers.ts:335): `var element = document.getElementById("${elementId}"); ${command}`
- **Desktop**: Never defined. `DesktopCodeEditorPresenter.InvokeScriptAsync(string)` passes the raw script to `CoreWebView2.ExecuteScriptAsync` — `element` is undefined, causing **silent failure** (caught at WebViewExtensions.cs:140)

This means ALL eval-based calls silently fail on desktop: `updateContent`, `updateLanguage`, `addAction`, `addCommand`, `focus`, `layout`, etc.

### Guard issue (secondary)
`AddActionAsync` (CodeEditor.Methods.cs:169) and `AddCommandAsync` (CodeEditor.Methods.cs:258) throw `PlatformNotSupportedException` when `_parentAccessor is null` on non-browser. After `InitialiseWebObjects()`, `_parentAccessor` IS set on desktop. The guard is misleading — it says "not yet supported on desktop" but the bridge infrastructure is complete.

### Bridge infrastructure IS complete
- `ParentAccessorDesktop.cs:286-304` — JSON-RPC targets for `callAction`, `callActionWithParameters`
- `Monaco.Helpers.ParentAccessor.ts:61-89` — desktop routes action callbacks through JSON-RPC
- `otherScriptsToBeOrganized.ts:80-101` — `addAction`/`addCommand` JS functions use `Accessor.callAction()`

The plumbing works end-to-end. Only the element reference in eval scripts is missing.

## Approach

### Step 1: Add unified `InvokeMethodAsync` to ICodeEditorPresenter

Add a single method that takes a method name and pre-serialized args. Each presenter handles element resolution internally — callers never reference `element`:

Signature: `Task<string> InvokeMethodAsync(string method, string[] serializedArgs)`

- **WASM** (`WasmCodeEditorPresenter`): Builds `method(element, args...)` and calls `NativeMethods.InvokeJS(ElementId, script)` — `InvokeJS` defines `element` via `document.getElementById(elementId)` per-call
- **Desktop** (`DesktopCodeEditorPresenter`): Wraps script to define `element` from the DOM (`editor-container` div, editor.html:28) and calls `CoreWebView2.ExecuteScriptAsync`

`ElementId` is already on `ICodeEditorPresenter` (L123). WASM returns a real DOM element ID; desktop returns a synthetic hash. The presenter knows which element to target — callers don't.

### Step 2: Migrate WebViewExtensions.cs — remove the `element` leak

Current code at WebViewExtensions.cs:95-147:
1. Serializes args to strings
2. Builds script: `method + "(element," + args + ")"` ← **the leak**
3. Calls `RunScriptAsync` → `_view.InvokeScriptAsync(script)`

Refactor to:
1. Serialize args to strings (keep — shared logic)
2. Call `_view.InvokeMethodAsync(method, serializedArgs)` ← **presenter handles element**
3. Deserialize result

The `method + "(element," + ...)` concatenation is removed entirely from `WebViewExtensions`.

### Step 3: Migrate raw `element` references in CodeEditor.Events.cs

These hardcoded scripts reference `element`:
- L165: `SendScriptAsync("EditorContext.getEditorForElement(element).editor.focus();")`
- L355: Same focus call
- L357: `SendScriptAsync("EditorContext.getEditorForElement(element).editor.layout();")`

Route through the new unified path. If no matching global function exists, the presenter's raw `InvokeScriptAsync(string)` can remain for the public API, but the presenter should wrap raw scripts with element definition too (so even the escape hatch works on desktop).

### Step 4: Remove ALL platform-specific guards from public API

In `CodeEditor.Methods.cs`:
- `AddActionAsync` (L169-185): Remove the `if (!OperatingSystem.IsBrowser())` branch. Keep `_parentAccessor is null` → `InvalidOperationException` (means "called before init" — same on both platforms).
- `AddCommandAsync` (L258-292): Same.
- Update XML doc comments: remove all "WASM bridge" / "desktop support not yet available" language.

Audit the rest of `CodeEditor.Methods.cs` and `CodeEditor.cs` for any other `OperatingSystem.IsBrowser()` guards or `PlatformNotSupportedException` throws on public API surface. If any exist, they should be removed or made internal.

### Step 5: Update documentation — remove "WASM only" notes

These docs explicitly tell developers to platform-check or skip features on desktop:

1. **`docs/getting-started.md:97`**: _"`AddActionAsync` and `AddCommandAsync` throw `PlatformNotSupportedException` on desktop because they require JSExport callbacks."_ → Remove entirely.

2. **`docs/getting-started.md:229-238`**: Troubleshooting section _"`PlatformNotSupportedException` on desktop"_ with `if (OperatingSystem.IsBrowser())` guard → Remove or replace with generic "called before EditorLoaded" troubleshooting.

3. **`docs/cookbook.md:681`**: _"Commands are WASM-only; on Desktop, lenses display but are not clickable"_ → Remove comment, remove `OperatingSystem.IsBrowser()` guard from example.

4. **`docs/cookbook.md:695-697`**: _"`AddCommandAsync` is WASM only (`PlatformNotSupportedException` on Desktop)."_ → Remove note. Replace with a single note about calling after `EditorLoaded`.

5. Any README platform matrix that lists AddAction/AddCommand as WASM-only → Update to show full platform support.

### Step 6: Add Linux and WSL2 prerequisites to getting-started.md

The current docs mention WebKitGTK only briefly (getting-started.md:12, L227). Add a dedicated **Desktop Prerequisites** section covering:

**Linux (native)**:
- WebKitGTK runtime: `sudo apt install libgtk-3-0t64 libwebkit2gtk-4.1-0` (Ubuntu 24.04+) or `sudo apt install libwebkit2gtk-4.0-37` (Ubuntu 22.04)
- The runtime error message from `DesktopCodeEditorPresenter.EnsureWebKitGtkAvailable()` (L408-429) provides install instructions, but docs should cover this upfront

**WSL2 on Windows 11**:
- WSLg requirement (Windows 11 22H2+ auto-includes WSLg for GUI app support)
- Same WebKitGTK packages as native Linux
- Environment variables: `DISPLAY=:0` (WSLg X11 — usually auto-set but explicit is safer), `GDK_GL=gles` (GLES rendering for WSL2 GPU compatibility)
- Reference the WSL2 launch profile from fn-10.4 (if implemented)

**Windows**:
- WebView2 Evergreen runtime (usually pre-installed on Windows 10 1803+)

**macOS**:
- No additional setup (uses built-in WKWebView)

## Key file references
- `ICodeEditorPresenter.cs:150` — current `InvokeScriptAsync(string)` (raw eval, no element)
- `ICodeEditorPresenter.cs:123` — `ElementId` property (available on both presenters)
- `WebViewExtensions.cs:133` — **the leak**: `method + "(element," + args + ");"`
- `WasmCodeEditorPresenter.cs:142-146` — WASM `InvokeScriptAsync` → `InvokeJS(ElementId, script)`
- `DesktopCodeEditorPresenter.cs:212-220` — Desktop `InvokeScriptAsync` → `ExecuteScriptAsync` (no element)
- `DesktopCodeEditorPresenter.cs:408-429` — `EnsureWebKitGtkAvailable()` runtime check
- `asyncCallbackHelpers.ts:334-337` — `InvokeJS` defines `element = document.getElementById(elementId)` per-call
- `editor.html:28` — `<div id="editor-container">` (desktop editor container)
- `CodeEditor.cs:366-416` — internal `InvokeScriptAsync` overloads (delegate to extension methods)
- `CodeEditor.Events.cs:165,355,357` — raw `SendScriptAsync` calls with hardcoded `element`
- `CodeEditor.Methods.cs:169-185` — AddActionAsync PlatformNotSupportedException
- `CodeEditor.Methods.cs:258-292` — AddCommandAsync PlatformNotSupportedException
- `ParentAccessorDesktop.cs:286-304` — JSON-RPC targets for callAction (already working)
- `docs/getting-started.md:97,229-238` — "WASM only" notes for AddAction/AddCommand
- `docs/cookbook.md:681,695-697` — "WASM only" notes for AddCommand in code lens example

## Acceptance
- [ ] New unified `InvokeMethodAsync` on `ICodeEditorPresenter`, implemented by both presenters
- [ ] `WebViewExtensions.cs` no longer builds scripts referencing `element`
- [ ] Raw `element` references removed from `CodeEditor.Events.cs` `SendScriptAsync` calls
- [ ] ALL `PlatformNotSupportedException` removed from `CodeEditor` public API (AddActionAsync, AddCommandAsync, any others)
- [ ] No `OperatingSystem.IsBrowser()` guards remain in public API code paths
- [ ] XML doc comments reflect full platform support
- [ ] `docs/getting-started.md` "WASM only" notes removed; desktop prerequisites section added (Linux/WSL2/Windows/macOS)
- [ ] `docs/cookbook.md` code lens example updated — no platform guard, no "WASM only" notes
- [ ] Action/command callback fires in C# when triggered from Monaco on desktop
- [ ] Existing WASM behavior unchanged (no regression)
- [ ] Solution builds: `dotnet build MonacoEditorComponent.slnx`
## Completion summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
