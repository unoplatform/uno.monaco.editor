# fn-11-extract-emitter-as-general-purpose-dts.2 Decouple emitter from Monaco specifics and create EmitterOptions

## Description
Decouple the extracted emitter from all Monaco-specific assumptions by introducing an `EmitterOptions` configuration object and extracting the TypeDoc URL generation into an optional `IDocLinkProvider` strategy.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp/Emitter/EmitterOptions.cs` (new)
- `tools/DtsSharp/DtsSharp/Emitter/IDocLinkProvider.cs` (new)
- `tools/DtsSharp/DtsSharp/Emitter/CSharpEmitter.cs` (modify — accept options, remove hardcoded values)
- `tools/DtsSharp/DtsSharp/Emitter/NameMapper.cs` (modify — configurable root namespace)

## Approach

**6 coupling points to address** (from research):

1. **`GetRepoRelativePath`** hardcodes `MonacoEditorComponent/Monaco/` — replace with `EmitterOptions.OutputPathPrefix` (default: `""`)
2. **`InterfaceToClassConverter` reference** at `CSharpEmitter.cs:345` — replace with `EmitterOptions.InterfaceConverterTypeName` (default: `null` to omit attribute)
3. **TypeDoc URL generation** (`WriteTypeDocRemarks`, `GetTypeDocUrl`, `GetTypeDocNamespacePrefix` + `_typeToSourceNamespace`/`_typeDocKinds` dictionaries) — extract to `IDocLinkProvider` interface with a single `string? GetDocUrl(string kind, string fullTypeName)` method. The Monaco implementation becomes an example. Emitter accepts `EmitterOptions.DocLinkProvider` (default: `null`)
4. **`NameMapper.ToCSharpNamespace`** root stripping — add `EmitterOptions.RootNamespace` that controls the C# namespace prefix (default: derive from first segment)
5. **`NameMapper.ToRelativeDirectory`** root stripping — same config as #4
6. **`FindToolDirectory` / ignore file discovery** in `Program.cs` — remove; ignore file is an explicit parameter

## Key context

- `EmitterOptions` should follow the .NET Options pattern: `sealed class` with `init` properties and sensible defaults
- The constructor of `CSharpEmitter` currently takes `(MonacoModel model, IgnoreList ignoreList, string outputRoot, string repoRoot)` — replace with `(TypeModel model, EmitterOptions options)`
- `TypeMapper` and `NameMapper` are currently `static` — keep static for now but ensure they accept configuration parameters where needed (e.g., `NameMapper.ToCSharpNamespace(string tsNamespace, string? rootNamespace)`)
## Acceptance
- [ ] `EmitterOptions` exists with properties: `OutputPathPrefix`, `InterfaceConverterTypeName`, `DocLinkProvider`, `RootNamespace`, `IgnoreFilePath`, `OutputDirectory`
- [ ] `IDocLinkProvider` interface extracted; TypeDoc-specific logic removed from core emitter
- [ ] `CSharpEmitter` constructor accepts `(TypeModel, EmitterOptions)` — no hardcoded Monaco strings remain
- [ ] Zero occurrences of `"Monaco"` in emitter output when `InterfaceConverterTypeName` and `DocLinkProvider` are null
- [ ] `NameMapper.ToCSharpNamespace` respects configurable root namespace
- [ ] All existing behavior preserved when options are configured to match Monaco defaults
- [ ] `dotnet build tools/DtsSharp/DtsSharp.slnx` succeeds
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
