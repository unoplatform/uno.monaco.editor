# fn-5-comprehensive-documentation-overhaul.7 Write getting started guide and API cookbook

## Description
Write a comprehensive getting started guide and API cookbook for uno.monaco.editor. Provide step-by-step tutorials and code examples for the most common developer scenarios on both WASM and Desktop targets.

**Size:** M
**Files:** `docs/getting-started.md`, `docs/cookbook.md`

## Approach

### Getting Started Guide (`docs/getting-started.md`)
Follow the Uno Platform how-to template structure:

1. **Prerequisites**: .NET 10 SDK, Uno Platform workloads (`wasm-tools` for WASM), `install-dependencies.ps1` for Monaco distribution
2. **Installation**: `dotnet add package Uno.Monaco.Editor`
3. **First Editor** (WASM): Minimal XAML + C# to display a CodeEditor with text and language
4. **First Editor** (Desktop): Same scenario showing desktop-specific setup (WebView2 requirement)
5. **Common Configuration**: Setting `Options` (via `StandaloneEditorConstructionOptions`), themes, read-only mode
6. **Troubleshooting**: Common issues (WebView not loading, Monaco distribution missing, platform-specific quirks)

### API Cookbook (`docs/cookbook.md`)
Recipe-style examples for common scenarios:

1. **Set text and language**: `CodeEditor.Text = "code"; CodeEditor.CodeLanguage = "csharp";`
2. **Listen to text changes**: `CodeEditor.PropertyChanged` on `Text` property
3. **Handle editor lifecycle**: `EditorLoading` vs `EditorLoaded` events, when to call methods
4. **Register a completion provider**: `CodeEditor.Languages.RegisterCompletionItemProviderAsync()` with callback
5. **Register a hover provider**: `CodeEditor.Languages.RegisterHoverProviderAsync()` with callback
6. **Add decorations and markers**: `DeltaDecorationsHelperAsync`, `SetModelMarkersAsync`
7. **Add custom actions**: `AddActionAsync` (WASM only — note desktop limitation)
8. **Theme management**: `SetThemeAsync`, respond to system theme changes
9. **Get/set cursor position**: `GetPositionAsync`, `SetPositionAsync`
10. **Navigate to a line**: `RevealLineAsync` variants
11. **Handle link clicks**: `OpenLinkRequested` event
12. **Error handling**: `InternalException` event, platform-specific error patterns

## Key context

- Each recipe should include working XAML + C# code and note any platform restrictions
- Cross-reference `docs/architecture.md` for understanding the interop model
- Reference upstream Monaco docs where the C# wrapper semantics match 1:1
- The `MonacoEditorTestApp/` project demonstrates many of these scenarios — use as reference
- `LanguagesHelper` is `[Obsolete]` — use `CodeEditor.Languages` accessor instead
- `CommandHandler` delegate receives `JsonElement` (not `JObject`) — show STJ deserialization patterns
## Acceptance
- [ ] `docs/getting-started.md` exists with prerequisites, installation, and first editor examples for both WASM and Desktop
- [ ] `docs/cookbook.md` exists with at least 10 recipe-style examples
- [ ] All code examples compile and are consistent with current API surface
- [ ] Platform-specific limitations noted per recipe (e.g., `AddActionAsync` desktop-unsupported)
- [ ] `LanguagesHelper` not used (deprecated) — examples use `CodeEditor.Languages`
- [ ] `CommandHandler` examples use `JsonElement` (not `JObject`)
- [ ] Cross-references to architecture docs and upstream Monaco TypeDoc API
- [ ] Troubleshooting section covers common setup issues
- [ ] Each recipe has both XAML and C# code snippets
- [ ] Lifecycle-dependent operations show proper event-based patterns (don't call before `EditorLoaded`)
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
