# fn-12-promote-editor-options-to.2 Update StandaloneEditorConstructionOptions to match Monaco 0.52.2

## Description
Update the hand-maintained `StandaloneEditorConstructionOptions` class to include all properties from the updated `IEditorOptions`, `IGlobalEditorOptions`, and `IStandaloneEditorConstructionOptions` interfaces.

**Size:** M
**Files:**
- `MonacoEditorComponent/Monaco/Editor/StandaloneEditorConstructionOptions.cs`

## Approach

- Follow the existing dictionary-backed INPC pattern at `StandaloneEditorConstructionOptions.cs` (`GetPropertyValue<T>` / `SetPropertyValue<T>`)
- Add ~48 new properties matching the interfaces updated in Task 1
- Remove 3 deprecated properties: `HighlightActiveIndentGuide`, `RenderIndentGuides`, `WordWrapMinified`
- For new sub-object types (e.g., `IGuidesOptions`, `IStickyScrollOptions`), use the generated interface types
- Ensure JSON property names use camelCase via `[JsonPropertyName("...")]` matching Monaco JS property names
- Ensure `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` is applied to nullable properties (follow existing pattern)

## Key context

- This file is in `.generator-ignore` — the emitter will never touch it
- The class implements `IStandaloneEditorConstructionOptions, INotifyPropertyChanged`
- All properties use the dictionary-backed pattern — no auto-properties
- Some new properties reference complex sub-object types (e.g., `Guides`, `StickyScroll`, `InlayHints`, `InlineSuggest`, `BracketPairColorization`, `UnicodeHighlight`, `Padding`). These types must exist from Task 1 generation
- Verify STJ serialization round-trips correctly for the new property types
## Acceptance
- [ ] `StandaloneEditorConstructionOptions` implements all properties from the updated interfaces
- [ ] 48 new properties added using existing `GetPropertyValue<T>` / `SetPropertyValue<T>` pattern
- [ ] 3 deprecated properties removed (`HighlightActiveIndentGuide`, `RenderIndentGuides`, `WordWrapMinified`)
- [ ] All new properties have correct `[JsonPropertyName]` attributes (camelCase matching Monaco)
- [ ] Nullable properties have `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds
- [ ] No STJ serialization errors when round-tripping new properties
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
