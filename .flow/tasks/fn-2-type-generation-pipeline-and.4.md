# fn-2-type-generation-pipeline-and.4 Rewrite domain converters for STJ

## Description
Rewrite the three non-enum custom Newtonsoft converters for STJ AND migrate `[JsonProperty]` attributes in files that contain converter definitions. This task owns all files with custom converter class definitions.

**Size:** M
**Files:** MonacoEditorComponent/Helpers/InterfaceToClassConverter.cs, MonacoEditorComponent/Monaco/Helpers/ICssStyle.cs (CssStyleConverter + model attributes), MonacoEditorComponent/Monaco/Helpers/CssGlyphStyle.cs, MonacoEditorComponent/Monaco/Helpers/CssInlineStyle.cs, MonacoEditorComponent/Monaco/Helpers/CssLineStyle.cs, MonacoEditorComponent/Monaco/Languages/ColorInformation.cs (ColorConverter + model attributes)

## Approach

- **InterfaceToClassConverter**: Rewrite as `JsonConverter<TInterface>` where `Read()` delegates to `JsonSerializer.Deserialize<TClass>(ref reader, options)` and `Write()` delegates to `JsonSerializer.Serialize(writer, value, typeof(TClass), options)`. No `Populate()` equivalent needed — STJ creates a new instance.
  - Current usage: `IRange->Range`, `IWordAtPosition->WordAtPosition` on properties in `FindMatch`, `ColorInformation`, `IModelDeltaDecoration`
  - Reference: BlazorMonaco `WorkspaceEditJsonConverter` for STJ interface deserialization pattern

- **CssStyleConverter**: Write-only converter. `Write()` outputs `ICssStyle.Name` as a JSON string. `Read()` throws `NotSupportedException`. In STJ, implement as `JsonConverter<ICssStyle>`.

- **ColorConverter**: Rewrite `Windows.UI.Color` <-> Monaco `{alpha, red, green, blue}` (0-1 floats) using `Utf8JsonReader`/`Utf8JsonWriter`. Map `reader.GetDouble()` for float values and `writer.WriteNumber()` for output.

- **Also in this task**: Migrate `[JsonProperty]` -> `[JsonPropertyName]` and `using` directives in `ICssStyle.cs`, `CssGlyphStyle.cs`, `CssInlineStyle.cs`, `CssLineStyle.cs`, and `ColorInformation.cs` (these files import Newtonsoft and participate in serialized model payloads).

- Add contract tests for:
  - InterfaceToClassConverter round-trip (IRange -> Range -> JSON -> Range)
  - ColorConverter round-trip (Color -> JSON -> Color with 0-1 float precision)
  - CssStyleConverter write-only (ICssStyle -> JSON string of Name)

## Key context

- Register converters in `MonacoJsonContext` via `[JsonConverter]` attributes on the types they convert, or add them to `Converters` collection on the context options.
- STJ custom converters use `ref Utf8JsonReader`/`Utf8JsonWriter` (not `JsonReader`/`JsonWriter`) — different token navigation API.
- CssStyleConverter is write-only: STJ source gen requires both Read/Write to compile, but `Read` can throw `NotSupportedException`.
- BlazorMonaco `WorkspaceEditJsonConverter` is a good reference for STJ interface deserialization pattern.

## Acceptance
- [ ] InterfaceToClassConverter rewritten as STJ `JsonConverter<TInterface>` with Read/Write
- [ ] CssStyleConverter rewritten as STJ `JsonConverter<ICssStyle>` (write-only, Read throws)
- [ ] ColorConverter rewritten as STJ `JsonConverter<Color>` using Utf8JsonReader/Writer
- [ ] `[JsonProperty]` attributes in ICssStyle.cs, CssGlyphStyle.cs, CssInlineStyle.cs, CssLineStyle.cs, and ColorInformation.cs migrated to `[JsonPropertyName]`
- [ ] All three converters registered via [JsonConverter] attributes
- [ ] No `serializer.Populate()` calls remain
- [ ] Contract tests for all three converters (round-trip or write-only as appropriate)
- [ ] Golden baseline tests still pass
- [ ] Build succeeds

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
