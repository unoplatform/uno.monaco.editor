# fn-2-type-generation-pipeline-and.6 Remove Newtonsoft.Json dependency and finalize build

## Description
Remove Newtonsoft.Json package dependency from the library, update build artifacts, and verify clean build across all targets. This task achieves zero-Newtonsoft in `MonacoEditorComponent/` (the library). Repo-wide cleanup (GenerateMonacoTypings/) is finalized in task 7.

<!-- Updated by plan-sync: fn-2-type-generation-pipeline-and.5 retained ~30+ Newtonsoft source-level references (dual-stack converters, [JsonConverter] on enums, [JsonIgnore], converter classes) for compatibility. These must be removed here, not just the package reference. Line numbers updated to match actual codebase. -->
**Size:** M
**Files:** Directory.Packages.props, MonacoEditorComponent/MonacoEditorComponent.csproj, MonacoEditorComponent/Properties/MonacoEditorComponent.rd.xml, changelog.md, MonacoEditorComponent/Helpers/InterfaceToClassConverter.cs, MonacoEditorComponent/Monaco/Helpers/ICssStyle.cs, MonacoEditorComponent/Monaco/Helpers/CssGlyphStyle.cs, MonacoEditorComponent/Monaco/Helpers/CssLineStyle.cs, MonacoEditorComponent/Monaco/Helpers/CssInlineStyle.cs, MonacoEditorComponent/Monaco/Languages/ColorInformation.cs, MonacoEditorComponent/Monaco/Selection.cs, MonacoEditorComponent/Monaco/Editor/*.cs (string-backed enums), MonacoEditorComponent/Monaco/Helpers/TextDecoration.cs

## Approach

- Remove `<PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />` from `Directory.Packages.props:9`
- Remove `<PackageReference Include="Newtonsoft.Json" />` from `MonacoEditorComponent.csproj:41`
- Update `MonacoEditorComponent.rd.xml` to remove Newtonsoft type directives (lines 15-24: both `NewtonsoftInterfaceToClassConverter` at 15-18 and `Newtonsoft.Json.JsonConvert` at 20-24) — these are no longer needed with STJ source gen
- Remove dual-stack Newtonsoft converter classes retained by fn-2.5:
  - `NewtonsoftInterfaceToClassConverter<TInterface, TClass>` in `InterfaceToClassConverter.cs` (the STJ `InterfaceToClassConverter<>` already exists in the same file)
  - `NewtonsoftCssStyleConverter` in `Monaco/Helpers/ICssStyle.cs` (the STJ `CssStyleConverter` already exists)
  - `NewtonsoftColorConverter` in `Monaco/Languages/ColorInformation.cs` (the STJ `ColorConverter` already exists)
- Remove all `[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]` attributes from string-backed enums (~20 files in Monaco/Editor/): Side.cs, AccessibilitySupport.cs, CursorBlinking.cs, AutoClosingQuotes.cs, Multiple.cs, FoldingStrategy.cs, SnippetSuggestions.cs, RenderLineHighlight.cs, SuggestSelection.cs, Show.cs, CursorSurroundingLinesStyle.cs, MultiCursorPaste.cs, AutoIndent.cs, ScrollbarBehavior.cs, AutoClosingBrackets.cs, CursorStyle.cs, MouseStyle.cs, AutoClosingOvertype.cs, MatchBrackets.cs, TabCompletion.cs, RenderWhitespace.cs, LineNumbersType.cs, AutoSurround.cs, InsertMode.cs, AutoFindInSelection.cs, MultiCursorModifier.cs, WordWrap.cs, WrappingIndent.cs, AcceptSuggestionOnEnter.cs. These enums already have STJ `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` attributes from fn-2.3.
- Remove `[Newtonsoft.Json.JsonConverter(typeof(NewtonsoftCssStyleConverter))]` from CssGlyphStyle.cs, CssLineStyle.cs, CssInlineStyle.cs
- Remove `[Newtonsoft.Json.JsonIgnore]` from Selection.cs and CssGlyphStyle.cs, CssLineStyle.cs (STJ `[JsonIgnore]` already present from fn-2.3)
- Remove `[Newtonsoft.Json.JsonConverter(typeof(NewtonsoftColorConverter))]` and `[Newtonsoft.Json.JsonConverter(typeof(NewtonsoftInterfaceToClassConverter<IRange, Range>))]` from ColorInformation.cs
- Remove `using Newtonsoft.Json;` from all affected .cs files
- Verify: `grep -r "Newtonsoft" MonacoEditorComponent/ --include="*.cs" --include="*.csproj" --include="*.props" --include="*.rd.xml"` returns zero results
- Build both targets: `dotnet build MonacoEditorTestApp -f net10.0-browserwasm` and `-f net10.0-desktop`
- Update `changelog.md` with breaking change entry:
  - Newtonsoft.Json transitive dependency removed
  - `CommandHandler` now receives `JsonElement` instead of `JObject`
  - Migration guidance for consumers

## Key context

- This is a breaking change for NuGet consumers who depend on the transitive Newtonsoft.Json dependency.
- The `rd.xml` runtime directives at `MonacoEditorComponent/Properties/MonacoEditorComponent.rd.xml` reference both `NewtonsoftInterfaceToClassConverter` (lines 15-18) and `Newtonsoft.Json.JsonConvert` (lines 20-24) — both are no longer needed.
- fn-2.5 intentionally retained Newtonsoft converter classes (`NewtonsoftInterfaceToClassConverter`, `NewtonsoftCssStyleConverter`, `NewtonsoftColorConverter`) and `[Newtonsoft.Json.JsonConverter]` attributes on ~30 enum/model files for dual-stack compatibility. These must all be removed in this task for the "zero Newtonsoft" acceptance criterion to pass.
- All STJ equivalents already exist from fn-2.3 (attributes) and fn-2.4 (converters), so removing the Newtonsoft versions should not break serialization.
- GenerateMonacoTypings/ currently has zero Newtonsoft references — that is already clean. Task 7's repo-wide cleanup step will be minimal.

## Acceptance
- [ ] Zero `Newtonsoft.Json` references in MonacoEditorComponent/ (library scope: *.cs, *.csproj, *.props, *.rd.xml)
- [ ] `rd.xml` updated (Newtonsoft directives removed)
- [ ] `changelog.md` documents the breaking change with migration guidance
- [ ] `dotnet build MonacoEditorTestApp -f net10.0-browserwasm` succeeds
- [ ] `dotnet build MonacoEditorTestApp -f net10.0-desktop` succeeds
- [ ] All serialization contract tests pass

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
