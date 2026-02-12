# fn-5-comprehensive-documentation-overhaul.5 Add XML documentation to hand-written public APIs

## Description
Add comprehensive XML documentation to all hand-written public APIs in MonacoEditorComponent. Follow `dotnet/runtime` ILogger-level quality: `<summary>`, `<param>`, `<returns>`, `<remarks>`, `<exception>` as appropriate. Use `<inheritdoc/>` for interface implementations.

**Size:** M
**Files:**
- `MonacoEditorComponent/CodeEditor/CodeEditor.cs` (main control, constructor, `SendScriptAsync`, `InvokeScriptAsync`, `Dispose`, `RenderingBackend`, `IsEditorLoaded`, `UriHelper`)
- `MonacoEditorComponent/CodeEditor/CodeEditor.Properties.cs` (9 DependencyProperties: `Text`, `SelectedText`, `SelectedRange`, `CodeLanguage`, `ReadOnly`, `Options`, `HasGlyphMargin`, `Decorations`, `Markers`)
- `MonacoEditorComponent/CodeEditor/CodeEditor.Events.cs` (5 events: `EditorLoading`, `EditorLoaded`, `OpenLinkRequested`, `InternalException`, `KeyDown`; lifecycle state machine)
- `MonacoEditorComponent/CodeEditor/CodeEditor.Methods.cs` (public methods: `RevealLineAsync` variants, `RevealPositionAsync`, `RevealRangeAsync`, `AddActionAsync`, `AddCommandAsync`, `CreateContextKeyAsync`, etc.)
- `MonacoEditorComponent/CodeEditor/ICodeEditorPresenter.cs` (presenter interface)
- `MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs`
- `MonacoEditorComponent/CodeEditor/WasmCodeEditorPresenter.cs`
- `MonacoEditorComponent/Bridge/BridgeContracts.cs` (11 DTO records + `BridgeSerializerContext`)
- `MonacoEditorComponent/Bridge/WebView2JsonRpcMessageHandler.cs` (security-critical)
- `MonacoEditorComponent/Serialization/MonacoJsonContext.cs`
- `MonacoEditorComponent/Extensions/WebViewExtensions.cs` (175 lines, currently 0 XML docs)
- `MonacoEditorComponent/Helpers/DebugLogger.cs`
- `MonacoEditorComponent/Helpers/ObservableVector.cs`
- `MonacoEditorComponent/Monaco/LanguagesHelper.cs` (marked `[Obsolete]`)
- **Plus any other hand-written files discovered during inventory**

## Approach

### Step 1: Public API inventory (discovery pass)
- Run `dotnet build MonacoEditorComponent.slnx /warnaserror:CS1591` to get a baseline of all undocumented public symbols
- Distinguish hand-written files from generated files (generated files are in `Monaco/Editor/`, `Monaco/Languages/`, `Monaco/Helpers/` — handle in task 6/8)
- Produce a list of all hand-written public symbols needing docs

### Step 2: Write XML docs
- Use standardized phrasing conventions:
  - Classes: `"Represents ..."` or `"Provides ..."`
  - Constructors: `"Initializes a new instance of the <see cref=\"ClassName\"/> class."`
  - Read-only properties: `"Gets ..."`; Read-write: `"Gets or sets ..."`
  - Boolean properties: `"Gets or sets a value indicating whether ..."`
  - Events: `"Occurs when ..."`
  - Methods: imperative verb describing the action
  - Boolean return values: `"true if [CONDITION]; otherwise, false."`
- Use `<remarks>` to note JS interop details (e.g., "Wraps the Monaco `editor.setModelLanguage` API")
- Use `<see href="..."/>` to cross-reference upstream Monaco TypeDoc API
- Document `<exception cref="PlatformNotSupportedException">` on methods that throw on desktop (`AddActionAsync`, `AddCommandAsync`)
- Document lifecycle contracts in event XML docs (e.g., "`EditorLoading` fires exactly once per load cycle")
- Fix existing doc errors: `HasGlyphMargin` summary says "Get or Set the CodeEditor Text" — correct it
- Use `<inheritdoc/>` for presenter implementations that mirror the interface
- Document serialization behavior in `WebViewExtensions` (how objects are marshaled to/from JSON, null handling)

### Step 3: Verify coverage
- Re-run `dotnet build MonacoEditorComponent.slnx /warnaserror:CS1591` — must pass with 0 warnings on hand-written files
- Suppress CS1591 on generated files via `#pragma warning disable CS1591` or `.editorconfig` scope if needed to isolate

## Key context

- `WebViewExtensions.cs` (175 lines) has zero XML docs and only 2 TODO comments — highest priority gap
- `WebView2JsonRpcMessageHandler.cs` is security-critical (method allowlist, payload size validation) — needs thorough `<remarks>`
- `MonacoJsonContext.cs` has `Relaxed` instance and `BuildTypeInfoMap()` — explain AOT context for consumers
- Many methods silently no-op when `_initialized` is false — document this behavior
- `UriHelper.AbsoluteUriString()` is public — verify if intentional, document accordingly
## Approach

- Use standardized phrasing conventions:
  - Classes: `"Represents ..."` or `"Provides ..."`
  - Constructors: `"Initializes a new instance of the <see cref=\"ClassName\"/> class."`
  - Read-only properties: `"Gets ..."`; Read-write: `"Gets or sets ..."`
  - Boolean properties: `"Gets or sets a value indicating whether ..."`
  - Events: `"Occurs when ..."`
  - Methods: imperative verb describing the action
- Use `<remarks>` to note JS interop details (e.g., "Wraps the Monaco `editor.setModelLanguage` API")
- Use `<see href="..."/>` to cross-reference upstream Monaco TypeDoc API
- Document `<exception cref="PlatformNotSupportedException">` on methods that throw on desktop (`AddActionAsync`, `AddCommandAsync`)
- Document lifecycle contracts in event XML docs (e.g., "`EditorLoading` fires exactly once per load cycle")
- Fix existing doc errors: `HasGlyphMargin` summary says "Get or Set the CodeEditor Text" — correct it
- Use `<inheritdoc/>` for presenter implementations that mirror the interface
- Document serialization behavior in `WebViewExtensions` (how objects are marshaled to/from JSON, null handling)

## Key context

- `WebViewExtensions.cs` (175 lines) has zero XML docs and only 2 TODO comments — highest priority gap
- `WebView2JsonRpcMessageHandler.cs` is security-critical (method allowlist, payload size validation) — needs thorough `<remarks>`
- `MonacoJsonContext.cs` has `Relaxed` instance and `BuildTypeInfoMap()` — explain AOT context for consumers
- Many methods silently no-op when `_initialized` is false — document this behavior
- `UriHelper.AbsoluteUriString()` is public — verify if intentional, document accordingly
## Acceptance
- [ ] Public API inventory completed — all hand-written public symbols catalogued
- [ ] 0 undocumented hand-written public symbols (verified via `dotnet build /warnaserror:CS1591` excluding generated files)
- [ ] All public classes have `<summary>` XML docs
- [ ] All public methods have `<summary>`, `<param>` for each parameter, `<returns>` for non-void
- [ ] All public properties have `<summary>` with correct phrasing ("Gets or sets ..." / "Gets ...")
- [ ] All public events have `<summary>` with "Occurs when ..." phrasing
- [ ] `<exception>` tags on methods that throw (`AddActionAsync`, `AddCommandAsync` → `PlatformNotSupportedException`)
- [ ] `<remarks>` on interop methods noting the wrapped Monaco JS API
- [ ] `<inheritdoc/>` used for presenter interface implementations
- [ ] `HasGlyphMargin` copy-paste doc error fixed
- [ ] `WebViewExtensions.cs` fully documented (was 0 XML docs)
- [ ] `WebView2JsonRpcMessageHandler.cs` has security-focused `<remarks>`
- [ ] `dotnet build MonacoEditorComponent.slnx /warnaserror:CS1591` passes (with generated files suppressed)
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
