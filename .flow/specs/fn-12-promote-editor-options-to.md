# Promote Editor Options to DependencyProperties and Sync Types to Monaco 0.52.2

## Overview

The CodeEditor control currently exposes only 5 pass-through DependencyProperties (`Text`, `CodeLanguage`, `ReadOnly`, `HasGlyphMargin`, `Options`). All other Monaco editor configuration lives on the `StandaloneEditorConstructionOptions` INPC bag, which is **invisible to XAML** — it cannot participate in Style Setters, TemplateBinding, VisualState Setters, or x:Bind.

Additionally, the C# type surface is **stale**: `IEditorOptions` has 103 properties vs Monaco 0.52.2's 148 (48 missing, 3 deprecated). `StandaloneEditorConstructionOptions` (hand-maintained, in `.generator-ignore`) has 118 properties and is similarly behind.

This epic:
1. Brings the auto-generated C# types up to date with Monaco 0.52.2
2. Updates the hand-maintained `StandaloneEditorConstructionOptions` to match
3. Promotes ~15 high-value options to top-level DependencyProperties on CodeEditor
4. Adds `updateOptions` debouncing to prevent excessive JS interop calls
5. Updates the test app and documentation

## Scope

**In scope:**
- Regenerate C# types from Monaco 0.52.2 `model.json`
- Update `StandaloneEditorConstructionOptions` (+48 properties, -3 deprecated)
- Promote ~15 commonly-bound properties to DPs (Theme, FontSize, FontFamily, WordWrap, LineNumbers, IsMinimapEnabled, TabSize, InsertSpaces, IsFoldingEnabled, RenderWhitespace, ScrollBeyondLastLine, RenderLineHighlight, CursorStyle, CursorBlinking, FontLigatures)
- Debounce `updateOptions` JS calls (~16ms / 1 frame via DispatcherQueue)
- Update test app XAML and documentation

**Out of scope:**
- Monaco version upgrade beyond 0.52.2 (separate epic)
- Source generator extraction (fn-11 — reverse dependency)
- Flattening sub-object properties (Minimap.*, Scrollbar.*, Hover.*) into individual DPs
- Renaming existing DPs (CodeLanguage, ReadOnly, HasGlyphMargin) — deferring breaking changes

## Approach

### Design decisions

1. **Options DP stays as escape hatch** — not deprecated. Individual DPs take precedence when explicitly set; unset (null) DPs defer to Options/Monaco defaults.
2. **Null defaults for nullable DPs** — use `ReadLocalValue` to distinguish "user set this" from "still at default". Follow pattern at `CodeEditor.cs:229-242`.
3. **Sub-objects get at most one convenience DP** (e.g., `IsMinimapEnabled`) — full sub-object config stays on Options.
4. **Debounce via DispatcherQueue** — aggregate rapid property changes into a single `updateOptions` JS call per frame.
5. **Bidirectional sync** follows existing pattern: DP callback → Options property; Options_PropertyChanged → sync DP if different + push to JS.

### Precedence rules

```
DP explicitly set (ReadLocalValue != UnsetValue) → DP value wins
DP at default (null / UnsetValue) → Options value wins
Neither set → Monaco JS default applies
```

### Properties to promote

| DP Name | Type | Monaco Property | Rationale |
|---------|------|-----------------|-----------|
| `Theme` | `string?` | `theme` | Top styling use case |
| `FontSize` | `double?` | `fontSize` | Accessibility binding |
| `FontFamily` | `string?` | `fontFamily` | Theme/brand customization |
| `WordWrap` | `WordWrap?` | `wordWrap` | Frequently toggled |
| `LineNumbers` | `LineNumbersType?` | `lineNumbers` | Common UI config |
| `IsMinimapEnabled` | `bool?` | `minimap.enabled` | Frequently toggled |
| `TabSize` | `int?` | `tabSize` | Developer preference |
| `InsertSpaces` | `bool?` | `insertSpaces` | Developer preference |
| `IsFoldingEnabled` | `bool?` | `folding` | Common toggle |
| `RenderWhitespace` | `RenderWhitespace?` | `renderWhitespace` | Common toggle |
| `ScrollBeyondLastLine` | `bool?` | `scrollBeyondLastLine` | Frequently customized |
| `RenderLineHighlight` | `RenderLineHighlight?` | `renderLineHighlight` | Visual preference |
| `CursorStyle` | `CursorStyle?` | `cursorStyle` | Styleable |
| `CursorBlinking` | `CursorBlinking?` | `cursorBlinking` | Styleable |
| `FontLigatures` | `bool?` | `fontLigatures` | Developer preference |

## Quick commands

```bash
# Regenerate types from model.json
npx tsx tools/monaco-type-extractor/src/index.ts -- node_modules/monaco-editor/monaco.d.ts -o tools/monaco-type-extractor/output/model.json
dotnet run --project tools/MonacoTypeEmitter -- --input tools/monaco-type-extractor/output/model.json --output MonacoEditorComponent/Monaco/

# Build solution
dotnet build MonacoEditorComponent.slnx --no-restore

# Build test app (WASM + Desktop)
dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm
dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop
```

## Acceptance

- [ ] `IEditorOptions.cs` has 148 properties (up from 103), matching Monaco 0.52.2
- [ ] 3 deprecated properties removed from C# interfaces (`HighlightActiveIndentGuide`, `RenderIndentGuides`, `WordWrapMinified`)
- [ ] `StandaloneEditorConstructionOptions` has all properties from updated interfaces
- [ ] ~15 new DPs on CodeEditor, each with bidirectional sync to Options
- [ ] `updateOptions` JS calls are debounced (single call per frame, not per-property)
- [ ] Setting a promoted DP in XAML works: `<monaco:CodeEditor Theme="vs-dark" FontSize="14" />`
- [ ] Style Setters work: `<Setter Property="Theme" Value="vs-dark" />`
- [ ] x:Bind works: `<monaco:CodeEditor FontSize="{x:Bind ViewModel.FontSize, Mode=TwoWay}" />`
- [ ] Null DP defers to Options value; explicit DP overrides Options
- [ ] Solution builds without warnings on all targets
- [ ] Test app exercises new DPs
- [ ] Documentation updated with new DP usage examples

## Dependencies

- **fn-6.4** (soft): DP type registration bugs on DecorationsProperty/MarkersProperty — fixing first ensures correct pattern for new DPs
- **fn-10** (mostly done, 8/9 tasks): Emitter fixes needed for clean type regeneration

## References

- `MonacoEditorComponent/CodeEditor/CodeEditor.Properties.cs` — current DP definitions
- `MonacoEditorComponent/CodeEditor/CodeEditor.cs:166-189` — Options_PropertyChanged handler
- `MonacoEditorComponent/Monaco/Editor/StandaloneEditorConstructionOptions.cs` — hand-maintained INPC class
- `MonacoEditorComponent/Monaco/Editor/IEditorOptions.cs` — auto-generated interface (stale)
- `tools/MonacoTypeEmitter/.generator-ignore` — files skipped by emitter
- hawkerm/monaco-editor-uwp — upstream pattern reference
- [Microsoft Learn: Custom DPs](https://learn.microsoft.com/en-us/windows/uwp/xaml-platform/custom-dependency-properties)
- [CommunityToolkit DispatcherQueueTimer.Debounce](https://learn.microsoft.com/en-us/dotnet/api/communitytoolkit.winui.ui.dispatcherqueuetimerextensions.debounce)
