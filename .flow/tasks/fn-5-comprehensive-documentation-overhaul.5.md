## Description
Add comprehensive XML documentation to all hand-written public APIs in MonacoEditorComponent. Follow `dotnet/runtime` ILogger-level quality. Use `<inheritdoc/>` for interface implementations.

**Size:** M
**Files:** `MonacoEditorComponent/CodeEditor/CodeEditor.cs`, `CodeEditor.Properties.cs`, `CodeEditor.Events.cs`, `CodeEditor.Methods.cs`, `ICodeEditorPresenter.cs`, `DesktopCodeEditorPresenter.cs`, `WasmCodeEditorPresenter.cs`, `Bridge/BridgeContracts.cs`, `Bridge/WebView2JsonRpcMessageHandler.cs`, `Serialization/MonacoJsonContext.cs`, `Extensions/WebViewExtensions.cs`, `Helpers/DebugLogger.cs`, `Helpers/ObservableVector.cs`, `Monaco/LanguagesHelper.cs`, plus any other hand-written files discovered during inventory.

## Approach

### Step 1: Public API inventory (discovery pass)
- Run `dotnet build MonacoEditorComponent.slnx /warnaserror:CS1591` for baseline of undocumented symbols
- Distinguish hand-written from generated files (generated: `Monaco/Editor/`, `Monaco/Languages/`, `Monaco/Helpers/`)
- Produce list of all hand-written public symbols needing docs

### Step 2: Write XML docs
- Phrasing conventions: Classes `"Represents/Provides ..."`, Properties `"Gets or sets ..."`, Boolean `"...a value indicating whether..."`, Events `"Occurs when ..."`, Methods: imperative verb
- `<remarks>` for JS interop details (e.g., "Wraps Monaco `editor.setModelLanguage`")
- `<see href="..."/>` to cross-reference upstream Monaco TypeDoc API
- `<exception cref="PlatformNotSupportedException">` on `AddActionAsync`, `AddCommandAsync`
- Lifecycle contracts in event docs ("`EditorLoading` fires exactly once per load cycle")
- Fix `HasGlyphMargin` copy-paste error at `CodeEditor.Properties.cs:137`
- `<inheritdoc/>` for presenter implementations
- Document serialization behavior in `WebViewExtensions`

### Step 3: Verify coverage
- `dotnet build MonacoEditorComponent.slnx /warnaserror:CS1591` must pass with 0 warnings on hand-written files
- Suppress CS1591 on generated files via `#pragma` or `.editorconfig` if needed

## Key context
- `WebViewExtensions.cs` (175 lines) has zero XML docs — highest priority gap
- `WebView2JsonRpcMessageHandler.cs` is security-critical (method allowlist, payload size validation)
- `MonacoJsonContext.cs` has `Relaxed` instance and `BuildTypeInfoMap()` — explain AOT context
- Many methods no-op when `_initialized` is false — document this behavior

## Acceptance
- [ ] Public API inventory completed — all hand-written public symbols catalogued
- [ ] 0 undocumented hand-written public symbols (verified via `dotnet build /warnaserror:CS1591` excluding generated files)
- [ ] All public classes have `<summary>`
- [ ] All public methods have `<summary>`, `<param>`, `<returns>` for non-void
- [ ] All public properties have correct phrasing
- [ ] All public events have "Occurs when ..." phrasing
- [ ] `<exception>` tags on methods that throw
- [ ] `<remarks>` on interop methods noting wrapped Monaco JS API
- [ ] `<inheritdoc/>` used for presenter implementations
- [ ] `HasGlyphMargin` doc error fixed
- [ ] `WebViewExtensions.cs` fully documented
- [ ] `WebView2JsonRpcMessageHandler.cs` has security-focused `<remarks>`
- [ ] `dotnet build MonacoEditorComponent.slnx /warnaserror:CS1591` passes (generated files suppressed)
