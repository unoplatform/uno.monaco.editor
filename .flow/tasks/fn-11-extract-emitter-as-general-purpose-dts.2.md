# fn-11-extract-emitter-as-general-purpose-dts.2 Decouple emitter from Monaco specifics, create EmitterOptions

## Description
Decouple the extracted emitter from all Monaco-specific assumptions by introducing an `EmitterOptions` configuration object, converting the emitter to a pure emit-to-memory API (zero I/O), and extracting TypeDoc URL generation into a configurable base URL. After this task, the emitter has zero hardcoded Monaco references.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp/Emitter/EmitterOptions.cs` (new)
- `tools/DtsSharp/DtsSharp/Emitter/CSharpEmitter.cs` (modify — pure API, accept options, remove hardcoded values)
- `tools/DtsSharp/DtsSharp/Emitter/NameMapper.cs` (modify — configurable root namespace)

## Approach

**5 coupling points to decouple:**

1. **`GetRepoRelativePath`** hardcodes `MonacoEditorComponent/Monaco/` — replace with `EmitterOptions.OutputPathPrefix` (default: `""`)
2. **`InterfaceToClassConverter` reference** at `CSharpEmitter.cs:345` — replace with `EmitterOptions.InterfaceConverterTypeName` (default: `null` to omit attribute)
3. **TypeDoc URL generation** — replace with `EmitterOptions.DocLinkBaseUrl` (string, nullable). Emitter constructs full URLs deterministically from base URL + type path. No `IDocLinkProvider` abstraction — just a simple URL string. Null = no doc links emitted.
4. **`NameMapper.ToCSharpNamespace`** root stripping — add `EmitterOptions.RootNamespace` (default: derive from first segment)
5. **`NameMapper.ToRelativeDirectory`** root stripping — same config as #4

**Pure emit-to-memory API:**
- Emitter must return `IReadOnlyList<(string hintName, string source)>` — zero I/O side effects.
- All `WriteFile`, `UpdateOnly`, file-path resolution logic must be removed from the emitter and moved into a test/CLI adapter if needed.
- The source generator pipeline (task 3) calls `context.AddSource()` with these pairs directly.

- `EmitterOptions`: sealed class with init properties. Emission concerns ONLY (not file I/O).
- Constructor changes from `(MonacoModel model, IgnoreList ignoreList, string outputRoot, string repoRoot)` to `(TypeModel model, IgnoreList ignoreList, EmitterOptions options)`. `IgnoreList` stays as a direct parameter.
- `TypeMapper` and `NameMapper` remain static but accept config parameters where needed.

## Key context

- Current emitter writes files to disk — this task must make it return strings instead.
- When `InterfaceConverterTypeName` is null, the `[JsonConverter]` attribute is omitted from generated interfaces.
- When `DocLinkBaseUrl` is null, no doc link comments are emitted.

## Acceptance
- [ ] `EmitterOptions` exists with emission-only properties: `OutputPathPrefix`, `InterfaceConverterTypeName`, `DocLinkBaseUrl`, `RootNamespace`
- [ ] No `IDocLinkProvider` abstraction — doc links use simple base URL string
- [ ] Emitter returns `IReadOnlyList<(string hintName, string source)>` — zero file I/O
- [ ] No file-writing logic in emitter (moved to adapter or removed)
- [ ] `CSharpEmitter` constructor accepts `(TypeModel, IgnoreList, EmitterOptions)` — no hardcoded Monaco strings
- [ ] No hardcoded Monaco-specific symbols, paths, or URLs in emitter implementation or defaults
- [ ] `NameMapper.ToCSharpNamespace` respects configurable root namespace
- [ ] All existing behavior preserved when options match Monaco defaults
- [ ] `dotnet build tools/DtsSharp/DtsSharp.slnx` succeeds

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
