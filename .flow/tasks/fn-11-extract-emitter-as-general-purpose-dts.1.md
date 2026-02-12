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

- Follow the existing project structure convention in `tools/` — see `tools/MonacoTypeEmitter/MonacoTypeEmitter.csproj`
- Create a new `.slnx` solution file under `tools/DtsSharp/`
- Target `net10.0` for the library (match repo's `global.json` SDK)
- Rename all namespaces: `MonacoTypeEmitter.Model` → `DtsSharp.Model`, `MonacoTypeEmitter.Emitter` → `DtsSharp.Emitter`
- Rename `MonacoModel` class → `TypeModel`, `MonacoNamespace` → `TypeNamespace`, etc.
- Keep all logic identical — this is a mechanical extraction + rename, NOT a refactor
- The intermediate JSON schema contract must remain backward compatible (same JSON deserializes into renamed C# types via `[JsonPropertyName]` if needed)

## Key context

- The model has 12 `TypeInfo` variants (discriminated union via `TypeInfoConverter.cs`)
- `TypeInfoConverter` uses `kind` field as discriminator — this must be preserved
- All model types are currently records — keep them as records
## Acceptance
- [ ] `tools/DtsSharp/DtsSharp.slnx` exists and `dotnet build` succeeds
- [ ] All source files compile with zero `Monaco` references in namespaces or public type names
- [ ] `MonacoModel` → `TypeModel` rename is complete across all model types
- [ ] Existing `model.json` from the ts-morph extractor deserializes correctly into renamed types
- [ ] No changes to `tools/MonacoTypeEmitter/` (original stays intact until task 6)
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
