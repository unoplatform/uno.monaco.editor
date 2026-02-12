# fn-11-extract-emitter-as-general-purpose-dts.2 Decouple emitter from Monaco specifics and create EmitterOptions

## Description
Decouple the extracted emitter from all Monaco-specific assumptions by introducing an `EmitterOptions` configuration object (emission concerns only) and extracting the TypeDoc URL generation into an optional `IDocLinkProvider` strategy.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp/Emitter/EmitterOptions.cs` (new)
- `tools/DtsSharp/DtsSharp/Emitter/IDocLinkProvider.cs` (new)
- `tools/DtsSharp/DtsSharp/Emitter/CSharpEmitter.cs` (modify — accept options, remove hardcoded values)
- `tools/DtsSharp/DtsSharp/Emitter/NameMapper.cs` (modify — configurable root namespace)

## Approach

**5 emission-related coupling points** (coupling point #6, ignore-file discovery, is handled by Task 3 CLI):

1. **`GetRepoRelativePath`** hardcodes `MonacoEditorComponent/Monaco/` — replace with `EmitterOptions.OutputPathPrefix` (default: `""`)
2. **`InterfaceToClassConverter` reference** at `CSharpEmitter.cs:345` — replace with `EmitterOptions.InterfaceConverterTypeName` (default: `null` to omit attribute)
3. **TypeDoc URL generation** — extract to `IDocLinkProvider` interface: `string? GetDocUrl(string kind, string fullTypeName)`. Emitter accepts `EmitterOptions.DocLinkProvider` (default: `null`).
4. **`NameMapper.ToCSharpNamespace`** root stripping — add `EmitterOptions.RootNamespace` (default: derive from first segment)
5. **`NameMapper.ToRelativeDirectory`** root stripping — same config as #4

## Key context

- `EmitterOptions`: `sealed class` with `init` properties. Emission concerns ONLY (not file I/O).
- Constructor changes from `(MonacoModel model, IgnoreList ignoreList, string outputRoot, string repoRoot)` to `(TypeModel model, IgnoreList ignoreList, EmitterOptions options)`. `IgnoreList` stays as a direct parameter since it's loaded by the caller (CLI or test harness).
- `TypeMapper` and `NameMapper` remain `static` but accept config parameters where needed.

## Acceptance
- [ ] `EmitterOptions` exists with emission-only properties: `OutputPathPrefix`, `InterfaceConverterTypeName`, `DocLinkProvider`, `RootNamespace`
- [ ] No file I/O concerns in `EmitterOptions`
- [ ] `IDocLinkProvider` interface extracted; TypeDoc-specific logic removed from core emitter
- [ ] `CSharpEmitter` constructor accepts `(TypeModel, IgnoreList, EmitterOptions)` — no hardcoded Monaco strings
- [ ] Zero occurrences of `"Monaco"` in emitter output when `InterfaceConverterTypeName` and `DocLinkProvider` are null
- [ ] `NameMapper.ToCSharpNamespace` respects configurable root namespace
- [ ] All existing behavior preserved when options match Monaco defaults
- [ ] `dotnet build tools/DtsSharp/DtsSharp.slnx` succeeds

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
