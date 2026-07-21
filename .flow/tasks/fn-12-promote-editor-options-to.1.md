# fn-12-promote-editor-options-to.1 Regenerate auto-generated C# types from Monaco 0.52.2 model.json

## Description
Re-run the type generation pipeline (ts-morph extractor + MonacoTypeEmitter) to bring auto-generated C# interfaces up to date with the vendored Monaco 0.52.2.

**Size:** M
**Files:**
- `MonacoEditorComponent/Monaco/Editor/IEditorOptions.cs` (103 → 148 properties)
- `MonacoEditorComponent/Monaco/Editor/IGlobalEditorOptions.cs` (+3 missing properties)
- `MonacoEditorComponent/Monaco/Editor/IStandaloneEditorConstructionOptions.cs` (+2 missing properties)
- New sub-object interface files (e.g., `IStickyScrollOptions.cs`, `IGuidesOptions.cs`, `IBracketPairColorizationOptions.cs`, etc.)
- Any new enum/type-alias files referenced by the 48 new properties

## Approach

1. Run the extractor: `npx tsx tools/monaco-type-extractor/src/index.ts -- node_modules/monaco-editor/monaco.d.ts -o tools/monaco-type-extractor/output/model.json`
2. Run the emitter: `dotnet run --project tools/MonacoTypeEmitter -- --input tools/monaco-type-extractor/output/model.json --output MonacoEditorComponent/Monaco/`
3. Verify output: check that `IEditorOptions.cs` now has 148 properties
4. Verify deprecated properties (`HighlightActiveIndentGuide`, `RenderIndentGuides`, `WordWrapMinified`) are absent from regenerated output
5. Build the solution to verify no compilation errors
6. If the emitter has bugs preventing clean generation, fix them in the emitter first

## Key context

- The emitter respects `.generator-ignore` — `StandaloneEditorConstructionOptions.cs` will NOT be updated (that's Task 2)
- The current `model.json` may already contain 0.52.2 data — check before re-extracting
- Some new properties reference sub-object types (e.g., `guides` → `IGuidesOptions`, `stickyScroll` → `IStickyScrollOptions`) that may not yet have C# counterparts — the emitter should generate these
- fn-10 emitter fixes (8/9 done) should be merged first
## Acceptance
- [ ] `IEditorOptions.cs` contains 148 properties matching Monaco 0.52.2
- [ ] `IGlobalEditorOptions.cs` includes `AutoDetectHighContrast`, `Theme`, `WordBasedSuggestionsOnlySameLanguage`
- [ ] `IStandaloneEditorConstructionOptions.cs` includes `AutoDetectHighContrast`, `AriaContainerElement`
- [ ] Deprecated properties (`HighlightActiveIndentGuide`, `RenderIndentGuides`, `WordWrapMinified`) do not appear in generated output
- [ ] New sub-object types referenced by the 48 new properties are generated (e.g., `IGuidesOptions`, `IStickyScrollOptions`, etc.)
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds without errors
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
