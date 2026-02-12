# fn-11-extract-emitter-as-general-purpose-dts.3 Wire IIncrementalGenerator pipeline and analyzer packaging

## Description
Wire the Roslyn `IIncrementalGenerator` that reads `.d.ts` from `AdditionalTextsProvider`, reads configuration from MSBuild properties via `AnalyzerConfigOptionsProvider`, parses → models → emits C# → `context.AddSource()`. Set up NuGet analyzer packaging layout. Implement structural equality on model types for incremental caching. Add exclusion support via `DtsSharp_IgnoreFile` MSBuild property.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp/DtsSharpGenerator.cs` (new — `IIncrementalGenerator` implementation)
- `tools/DtsSharp/DtsSharp/Model/TypeModel.cs` (modify — `ImmutableArray<T>` collections, structural equality)
- `tools/DtsSharp/DtsSharp/DtsSharp.csproj` (modify — analyzer packaging properties)

## Approach

**Generator wiring:**
- Implement `IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext)`
- `context.AdditionalTextsProvider` → filter for `.d.ts` extension → `Select` content
- **Multiple `.d.ts` files:** Each file is processed independently (no cross-file resolution). Generator emits types per file.
- `context.AnalyzerConfigOptionsProvider.GlobalOptions` → read MSBuild properties:
  - `build_property.DtsSharp_RootNamespace` → `EmitterOptions.RootNamespace`
  - `build_property.DtsSharp_ConverterType` → `EmitterOptions.InterfaceConverterTypeName`
  - `build_property.DtsSharp_DocLinkBaseUrl` → `EmitterOptions.DocLinkBaseUrl`
  - `build_property.DtsSharp_OutputPathPrefix` → `EmitterOptions.OutputPathPrefix`
  - `build_property.DtsSharp_IgnoreFile` → path to ignore file (read via `AdditionalTextsProvider` or filesystem)
- Combine `.d.ts` content + options → parse → model → emit → `context.AddSource(hintName, sourceText)`
- One `AddSource` call per generated file (matching emitter's per-file output)

**Exclusion support:**
- `DtsSharp_IgnoreFile` MSBuild property points to an ignore file (same format as current `.generator-ignore`)
- Load the ignore file content at generation time and pass to emitter as `IgnoreList`
- Exclusion is essential for migration (Monaco has hand-authored files that must not be regenerated)

**Incremental caching (structural equality):**
- **Replace `List<T>` with `ImmutableArray<T>`** in all model types — `List<T>` has reference equality which breaks Roslyn's incremental comparison
- Model types must implement structural deep equality (`IEquatable<T>`) or use records with custom collection equality
- Consider using a content hash of the `.d.ts` text as the pipeline boundary value (simple, correct, avoids deep model comparison)
- **`IsExternalInit` polyfill** needed if using records on `netstandard2.0`
- **`ImmutableArray<T>` on `netstandard2.0`** requires `System.Collections.Immutable` NuGet package reference

**NuGet packaging:**
- Set in `.csproj`: `<IsRoslynComponent>true</IsRoslynComponent>` or manually configure `analyzers/dotnet/cs/` pack layout
- `DtsSharp.Runtime` should be a package dependency that consumers automatically get
- Reference pattern: [Mapperly](https://github.com/riok/mapperly) for production source generator packaging

## Key context

- The emitter (after task 2) returns `IReadOnlyList<(string hintName, string source)>` — the generator just iterates and calls `context.AddSource()`.
- Roslyn incremental generators must be deterministic and side-effect-free.
- The simplest correct caching approach may be: hash `.d.ts` content + serialized options as the pipeline key, and if unchanged, skip re-emission entirely.

## Acceptance
- [ ] `DtsSharpGenerator` implements `IIncrementalGenerator`
- [ ] Reads `.d.ts` files from `AdditionalTextsProvider` — multiple files processed independently
- [ ] Reads MSBuild properties from `AnalyzerConfigOptionsProvider.GlobalOptions`
- [ ] `DtsSharp_IgnoreFile` property loads exclusion list and filters generated types
- [ ] Pipeline: parse → model → emit → `context.AddSource()`
- [ ] Model types use `ImmutableArray<T>` (not `List<T>`) for collections
- [ ] Structural equality implemented — unchanged `.d.ts` skips re-emission
- [ ] `IsExternalInit` polyfill included for `netstandard2.0` record support
- [ ] `System.Collections.Immutable` referenced for `ImmutableArray<T>` on ns2.0
- [ ] `.csproj` configured for analyzer NuGet packaging (`analyzers/dotnet/cs/`)
- [ ] `DtsSharp.Runtime` referenced as package dependency
- [ ] `dotnet build tools/DtsSharp/DtsSharp.slnx` succeeds

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
