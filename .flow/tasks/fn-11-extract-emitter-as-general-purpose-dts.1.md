# fn-11-extract-emitter-as-general-purpose-dts.1 Scaffold standalone library solution and extract model types

## Description
Create the standalone solution structure under `tools/DtsSharp/` and extract the model types from `MonacoTypeEmitter` into a generic namespace.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp.slnx` (new)
- `tools/DtsSharp/DtsSharp/DtsSharp.csproj` (new — core library)
- `tools/DtsSharp/DtsSharp/Model/TypeModel.cs` (extracted from `MonacoModel.cs`)
- `tools/DtsSharp/DtsSharp/Model/TypeInfoConverter.cs` (extracted)
- `tools/DtsSharp/DtsSharp/Emitter/CSharpEmitter.cs` (extracted)
- `tools/DtsSharp/DtsSharp/Emitter/TypeMapper.cs` (extracted)
- `tools/DtsSharp/DtsSharp/Emitter/NameMapper.cs` (extracted)
- `tools/DtsSharp/DtsSharp/Emitter/IgnoreList.cs` (extracted)

## Approach

- Follow the existing project structure convention in `tools/`
- Create a new `.slnx` solution file under `tools/DtsSharp/`
- Target `net10.0` (match repo's `global.json` SDK)
- Rename namespaces: `MonacoTypeEmitter.Model` → `DtsSharp.Model`, `MonacoTypeEmitter.Emitter` → `DtsSharp.Emitter`
- **Rename only the top-level model type** (current code uses classes):
  - `MonacoModel` → `TypeModel` (class with properties: `SchemaVersion`, `ExtractedAt`, `SourceFile`, `Namespaces`)
  - All other type names preserved: `NamespaceInfo`, `InterfaceInfo`, `ClassInfo`, `EnumInfo`, `TypeAliasInfo`, `FunctionInfo`, `MethodInfo`, `PropertyInfo`, `ParameterInfo`, `TypeParameterInfo`, `TypeInfo` (12 variants)
- Mechanical extraction + rename, NOT a refactor
- Preserve **class-based** model structure exactly as in current `MonacoModel.cs`
- JSON schema backward compatible (same JSON deserializes into renamed types)

## Key context

- Model types are **classes** — see `tools/MonacoTypeEmitter/Model/MonacoModel.cs`
- `TypeInfoConverter` uses `kind` discriminator for 12-variant `TypeInfo` union — preserve exactly
- `TypeModel` properties: `SchemaVersion`, `ExtractedAt`, `SourceFile`, `Namespaces` (all from current `MonacoModel`)

## Acceptance
- [ ] `tools/DtsSharp/DtsSharp.slnx` exists and `dotnet build` succeeds
- [ ] Zero `Monaco` references in namespaces or public type names
- [ ] `MonacoModel` → `TypeModel` rename complete; includes `SourceFile` property
- [ ] All other model class names preserved
- [ ] Class-based model structure preserved
- [ ] Existing `model.json` deserializes correctly into renamed types
- [ ] `TypeInfoConverter` discriminator logic unchanged
- [ ] No changes to `tools/MonacoTypeEmitter/`

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
