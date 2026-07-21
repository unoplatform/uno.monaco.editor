# fn-2-type-generation-pipeline-and.1 Create MonacoJsonContext and STJ serialization infrastructure

## Description
Create the foundational STJ source-generation context and shared serialization infrastructure that all subsequent tasks depend on. Also scaffold serialization contract test infrastructure with golden baselines from current Newtonsoft behavior.

**Size:** M
**Files:** MonacoEditorComponent/Serialization/MonacoJsonContext.cs (new), MonacoEditorComponent/MonacoEditorComponent.csproj, test project (contract test scaffolding)

## Approach

- Create `MonacoJsonContext : JsonSerializerContext` with `[JsonSourceGenerationOptions]`:
  - `PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase`
  - `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`
  - **Do NOT set `UseStringEnumConverter = true`** — Monaco has numeric enums (MarkerSeverity, CompletionItemKind, TrackedRangeStickiness, etc.) that must stay numeric. String enum conversion is opt-in per-enum only (task 2).
- Register all types that cross the JS interop boundary via `[JsonSerializable]`: Position, Range, Selection, CompletionItem, CompletionList, CompletionContext, CodeAction, CodeActionList, CodeActionContext, CodeLens, CodeLensList, Hover, IMarkdownString, ColorInformation, ColorPresentation, IMarkerData, Marker, StandaloneEditorConstructionOptions, IModelDeltaDecoration, IModelDecorationOptions, and collection variants (arrays, lists)
- Add to .csproj: `<JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>`
- Follow Uno Platform pattern from `unoplatform/uno` `FontManifest.cs`
- Context should be `internal partial class` in new `MonacoEditorComponent/Serialization/` directory
- **Golden baseline tests**: Before migrating any types, capture Newtonsoft serialization output for key payloads (Position, Range, Selection, CompletionItem, CodeAction, Hover, ColorInformation, MarkerData). Save as golden fixtures. After migration, STJ output must match these baselines exactly.
- Scaffold contract test infrastructure with at least one round-trip test per major type category (primitive, enum, model)

## Key context

- fn-1 task 5 creates a separate `BridgeSerializerContext` for desktop bridge DTOs. This context is for Monaco model types. They can be combined later via `JsonTypeInfoResolver.Combine()`.
- Use `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` — STJ escapes `<`, `>`, `&` by default unlike Newtonsoft, and Monaco content includes code.
- Numeric enums (MarkerSeverity=1/2/4/8, CompletionItemKind=0-27, TrackedRangeStickiness=0-3) must serialize as integers. String enums (CursorBlinking="blink", WordWrap="on") must serialize as strings. The context does NOT set a global enum policy.

## Acceptance
- [ ] `MonacoJsonContext` partial class exists with `[JsonSourceGenerationOptions]` (NO `UseStringEnumConverter`)
- [ ] All ~25 cross-boundary types registered via `[JsonSerializable]`
- [ ] `JsonSerializerIsReflectionEnabledByDefault` set to `false` in .csproj
- [ ] Golden baseline fixtures captured from Newtonsoft for Position, Range, Selection, CompletionItem, CodeAction, Hover, ColorInformation, MarkerData
- [ ] Serialization contract test scaffolding with round-trip tests per type category
- [ ] Numeric enums (MarkerSeverity, CompletionItemKind) serialize as integers
- [ ] `dotnet build MonacoEditorComponent.slnx` succeeds (existing Newtonsoft code untouched)

## Done summary
Created MonacoJsonContext STJ source-gen context registering ~25 cross-boundary Monaco types with CamelCase naming, WhenWritingNull, and UnsafeRelaxedJsonEscaping. Added JsonSerializerIsReflectionEnabledByDefault=false to .csproj. Created 21 serialization contract tests covering golden Newtonsoft baselines (exact JSON match), STJ round-trip per type category, numeric enum preservation, camelCase naming, null omission, relaxed encoder behavior, and Uri collision safety.
## Evidence
- Commits: 4bc5c768622b1cf25dcddc58eaf4f3b1e208e70b, 08f62e0e0bab19cbd481182931afb2ffb39867a9
- Tests: dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj --filter-trait Category=Serialization, dotnet build MonacoEditorComponent.slnx --no-restore
- PRs: