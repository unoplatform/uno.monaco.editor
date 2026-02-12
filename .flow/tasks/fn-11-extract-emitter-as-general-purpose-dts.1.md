# fn-11-extract-emitter-as-general-purpose-dts.1 Scaffold solution, extract model/emitter, create runtime package

## Description
Create the standalone solution structure under `tools/DtsSharp/`, extract model types and emitter from `MonacoTypeEmitter` into a `netstandard2.0` library, and create the `DtsSharp.Runtime` companion package. This task is purely mechanical extraction — no decoupling or refactoring.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp.slnx` (new)
- `tools/DtsSharp/DtsSharp/DtsSharp.csproj` (new — targets `netstandard2.0`)
- `tools/DtsSharp/DtsSharp/Model/TypeModel.cs` (extracted from `MonacoModel.cs`)
- `tools/DtsSharp/DtsSharp/Model/TypeInfoConverter.cs` (extracted)
- `tools/DtsSharp/DtsSharp/Emitter/CSharpEmitter.cs` (extracted)
- `tools/DtsSharp/DtsSharp/Emitter/TypeMapper.cs` (extracted)
- `tools/DtsSharp/DtsSharp/Emitter/NameMapper.cs` (extracted)
- `tools/DtsSharp/DtsSharp/Emitter/IgnoreList.cs` (extracted)
- `tools/DtsSharp/DtsSharp.Runtime/DtsSharp.Runtime.csproj` (new — targets `netstandard2.0`)
- `tools/DtsSharp/DtsSharp.Runtime/InterfaceToClassConverter.cs` (extracted from `MonacoEditorComponent/Helpers/`)

## Approach

- Create `tools/DtsSharp/DtsSharp.slnx` with two projects
- **DtsSharp** (`netstandard2.0`): core library containing model + emitter. Add `Microsoft.CodeAnalysis.CSharp` package reference (needed later for source gen, but the project targets ns2.0 from the start)
- **DtsSharp.Runtime** (`netstandard2.0`): extract `InterfaceToClassConverter<TInterface, TClass>` from `MonacoEditorComponent/Helpers/InterfaceToClassConverter.cs` into `DtsSharp.Runtime` namespace. **Must be `public`** (currently `internal` in Monaco — consumer-generated code needs cross-assembly access)
- Rename namespaces: `MonacoTypeEmitter.Model` → `DtsSharp.Model`, `MonacoTypeEmitter.Emitter` → `DtsSharp.Emitter`
- **Rename only the top-level model type**: `MonacoModel` → `TypeModel` (class with properties: `SchemaVersion`, `ExtractedAt`, `SourceFile`, `Namespaces`)
- All other type names preserved: `NamespaceInfo`, `InterfaceInfo`, `ClassInfo`, `EnumInfo`, `TypeAliasInfo`, `FunctionInfo`, `MethodInfo`, `PropertyInfo`, `ParameterInfo`, `TypeParameterInfo`, `TypeInfo` (13 variants: primitive, reference, union, intersection, array, tuple, literal, function, objectLiteral, indexedAccess, typeOperator, conditional, intrinsic)
- Mechanical extraction + rename, NOT a refactor
- Preserve **class-based** model structure exactly as in current `MonacoModel.cs`
- JSON schema backward compatible (same JSON deserializes into renamed types)

## Key context

- Model types are **classes** — see `tools/MonacoTypeEmitter/Model/MonacoModel.cs`
- `TypeInfoConverter` uses `kind` discriminator for **13-variant** `TypeInfo` union — preserve exactly
- `TypeModel` properties: `SchemaVersion`, `ExtractedAt`, `SourceFile`, `Namespaces` (all from current `MonacoModel`)
- `InterfaceToClassConverter` is currently `internal` in `MonacoEditorComponent/Helpers/` — must become `public` in `DtsSharp.Runtime`

## Acceptance
- [ ] `tools/DtsSharp/DtsSharp.slnx` exists and `dotnet build` succeeds
- [ ] Zero `Monaco` references in namespaces or public type names
- [ ] `MonacoModel` → `TypeModel` rename complete; includes `SourceFile` property
- [ ] All other model class names preserved
- [ ] Class-based model structure preserved
- [ ] `TypeInfo` has 13 variants (including `intrinsic`) — all preserved
- [ ] Existing `model.json` deserializes correctly into renamed types
- [ ] `TypeInfoConverter` discriminator logic unchanged
- [ ] `InterfaceToClassConverter` is `public` in `DtsSharp.Runtime` namespace
- [ ] External consumer compile test: generated code with `[JsonConverter(typeof(InterfaceToClassConverter<,>))]` compiles against `DtsSharp.Runtime`
- [ ] No changes to `tools/MonacoTypeEmitter/` (original stays intact)

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
