# fn-2-type-generation-pipeline-and.3 Migrate Monaco model JsonProperty attributes to STJ

## Description
Bulk-migrate `[JsonProperty]` attributes to `[JsonPropertyName]` across all Monaco model type files. Strictly excludes converter files (handled by task 4) and enum files (handled by task 2).

**Size:** M (mechanical bulk change across ~40 model files, two phases)
**Files:** MonacoEditorComponent/Monaco/Editor/*.cs (model/interface files only — NOT enum files, NOT converter files), Monaco/Languages/*.cs (NOT ColorInformation.cs — task 4), Monaco/*.cs (Position, Range, Selection, Uri, etc.)

## Dependencies and ordering

**This task depends on task 4** (domain converter rewrite). Reason: Files like `FindMatch.cs`, `IModelDeltaDecoration.cs`, and `IWordAtPosition.cs` use `[JsonConverter(typeof(InterfaceToClassConverter<,>))]`. Task 4 must rewrite `InterfaceToClassConverter` for STJ first, so that when task 3 migrates these files' `[JsonProperty]` attributes and `using` directives, the STJ converter is already available and the build succeeds.

Execution order: task 1 → tasks 2, 4, 7 (parallel) → task 3 → task 5 → task 6

## Strict file boundaries

**This task handles:** Model types, interface types, options types — files containing `[JsonProperty]` attributes but NO custom `JsonConverter` class definitions. This includes files that *reference* converters (e.g., `[JsonConverter(typeof(InterfaceToClassConverter<IRange, Range>))]` on a property) — those converter attributes are updated to reference the STJ-rewritten converters from task 4.

**This task does NOT handle:**
- Files with enum converter classes → task 2
- `ICssStyle.cs` (contains CssStyleConverter definition) → task 4
- `CssGlyphStyle.cs`, `CssInlineStyle.cs`, `CssLineStyle.cs` → task 4
- `ColorInformation.cs` (contains ColorConverter definition) → task 4
- `InterfaceToClassConverter.cs` (contains converter definition) → task 4

## Approach — Two phases

**Phase A: Mechanical rewrite**
- Replace `[JsonProperty("name")]` with `[JsonPropertyName("name")]`
- For properties where global CamelCase naming policy produces the correct wire name, the `[JsonPropertyName]` can be omitted. Only keep explicit `[JsonPropertyName]` for names that diverge.
- Remove per-property `NullValueHandling = NullValueHandling.Ignore` — handled globally by `DefaultIgnoreCondition = WhenWritingNull`
- Replace `using Newtonsoft.Json` with `using System.Text.Json.Serialization`
- Handle `[JsonIgnore]` — same attribute name, different namespace

**Phase B: Semantic correctness**
- Audit ALL deserialized types for non-public setters. Known cases:
  - `Range`: `StartLineNumber`, `StartColumn`, `EndLineNumber`, `EndColumn` — all `private set`
  - `Position`: `LineNumber`, `Column` — all `private set`
  - `Selection`: inherits Range + `SelectionStartLineNumber`, etc. — check setter accessibility
  - `WordAtPosition`: `Word`, `StartColumn`, `EndColumn` — check setter accessibility
  - Any other type deserialized from JS via `JsonSerializer.Deserialize<T>`
- Add `[JsonInclude]` on each property with non-public setter, OR refactor to use `[JsonConstructor]`-annotated constructor
- For properties that currently do NOT have `NullValueHandling.Ignore`, verify they should serialize nulls. If so, add `[JsonIgnore(Condition = JsonIgnoreCondition.Never)]`.
- **Use concrete DTO annotations on class properties**, not just interface members. STJ relies on concrete type attributes for serialization. For properties typed as interfaces (`IRange`, `IPosition`), ensure the concrete classes have proper `[JsonPropertyName]` or rely on CamelCase naming policy.
- Add tests for interface-typed property serialization/deserialization (e.g., a property of type `IRange` that holds a `Range` instance — verify correct round-trip).

## Key context

- STJ is case-sensitive by default. The global `CamelCase` naming policy covers most cases; also set `PropertyNameCaseInsensitive = true` on options for deserialization from JS.
- STJ uses concrete type attributes for serialization, NOT interface attributes. Place `[JsonPropertyName]` on concrete class properties. Interface attributes serve as documentation only.
- Validate golden baselines (from task 1) still match after migration.

## Acceptance
- [ ] Zero `[JsonProperty]` attributes in files handled by this task
- [ ] Model types use `[JsonPropertyName]` (only where needed) or rely on CamelCase naming policy
- [ ] ALL deserialized types audited for non-public setters (not just Range/Position)
- [ ] Non-public setter properties have `[JsonInclude]` or constructor-based deserialization
- [ ] `using Newtonsoft.Json` removed from all modified model files
- [ ] Properties requiring null serialization have explicit `[JsonIgnore(Condition = JsonIgnoreCondition.Never)]`
- [ ] Tests for interface-typed property round-trip (IRange, IPosition)
- [ ] No changes to files owned by task 2 (enums) or task 4 (converter files)
- [ ] Golden baseline tests still pass
- [ ] Build succeeds

## Done summary
Bulk-migrated [JsonProperty] attributes from Newtonsoft.Json to System.Text.Json across 64 Monaco model files, added [JsonInclude] with internal setters for deserialization, dual-stack [JsonIgnore] on Selection.Direction, [JsonIgnore(Condition=Never)] on Model for explicit null, and comprehensive round-trip tests for IRange and IPosition.
## Evidence
- Commits: 23625ce, 9b6e9b8
- Tests: dotnet build MonacoEditorComponent.slnx --no-restore, dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj --no-build -- --filter-class MonacoEditorComponent.Tests.Serialization.SerializationContractTests
- PRs: