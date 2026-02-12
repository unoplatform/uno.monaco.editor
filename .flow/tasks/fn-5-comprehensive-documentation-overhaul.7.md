## Description
Write a comprehensive getting started guide and API cookbook for uno.monaco.editor. Step-by-step tutorials and code examples for common scenarios on both WASM and Desktop targets.

**Size:** M
**Files:** `docs/getting-started.md`, `docs/cookbook.md`

## Approach

### Getting Started Guide (`docs/getting-started.md`)
1. **Prerequisites**: .NET 10 SDK, Uno Platform workloads (`wasm-tools` for WASM), `install-dependencies.ps1`
2. **Installation**: `dotnet add package Uno.Monaco.Editor`
3. **First Editor (WASM)**: Minimal XAML + C# to display a CodeEditor
4. **First Editor (Desktop)**: Same scenario with desktop-specific setup (WebView2)
5. **Common Configuration**: `StandaloneEditorConstructionOptions`, themes, read-only mode
6. **Troubleshooting**: Common issues (WebView not loading, Monaco distribution missing)

### API Cookbook (`docs/cookbook.md`)
12 recipe-style examples covering: set text/language, listen to changes, lifecycle events, completion providers, hover providers, decorations/markers, custom actions, themes, cursor position, line navigation, link clicks, error handling.

### Validation strategy
- **Verify Monaco version** from root `package.json` and `node_modules/monaco-editor/package.json` before referencing
- Cross-reference code snippets against `MonacoEditorTestApp/` (demonstrates many scenarios)
- XAML snippets must match current namespace and control names
- C# snippets must use current API signatures (`JsonElement` not `JObject`)
- Build `MonacoEditorTestApp` for both targets to confirm documented setup works:
  `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm`
  `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop`

## Key context
- `LanguagesHelper` is `[Obsolete]` — examples must use `CodeEditor.Languages`
- `CommandHandler` delegate receives `JsonElement` (not `JObject`)
- Cross-reference `docs/architecture.md` for interop model understanding

## Acceptance
- [ ] `docs/getting-started.md` exists with prerequisites, installation, and first editor examples for both targets
- [ ] `docs/cookbook.md` exists with at least 10 recipe-style examples
- [ ] All code snippets validated against `MonacoEditorTestApp/` and current API signatures
- [ ] XAML snippets use correct namespace and control names
- [ ] Platform-specific limitations noted per recipe
- [ ] `LanguagesHelper` not used (deprecated) — examples use `CodeEditor.Languages`
- [ ] `CommandHandler` examples use `JsonElement`
- [ ] Monaco version verified from `package.json` before referencing
- [ ] Cross-references to architecture docs and upstream Monaco TypeDoc API
- [ ] Troubleshooting section covers common setup issues
- [ ] `MonacoEditorTestApp` builds for both targets with documented instructions
