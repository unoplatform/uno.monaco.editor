# fn-11-extract-emitter-as-general-purpose-dts.3 Wire IIncrementalGenerator pipeline and analyzer packaging

## Description
Wire the Roslyn `IIncrementalGenerator` that reads `.d.ts` from `AdditionalTextsProvider`, reads configuration from MSBuild properties via `AnalyzerConfigOptionsProvider`, parses → models → emits C# → `context.AddSource()`. Set up NuGet analyzer packaging layout. Implement structural equality on model types for incremental caching. Add exclusion support via ignore file loaded through `AdditionalTextsProvider`.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp/DtsSharpGenerator.cs` (new — `IIncrementalGenerator` implementation)
- `tools/DtsSharp/DtsSharp/Model/TypeModel.cs` (modify — `ImmutableArray<T>` collections, structural equality)
- `tools/DtsSharp/DtsSharp/DtsSharp.csproj` (modify — analyzer packaging properties)

## Approach

**Generator wiring:**
- Implement `IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext)`
- `context.AdditionalTextsProvider` → filter for `.d.ts` extension → `Select` content
- **Multiple `.d.ts` files:** Each file is processed independently (no cross-file resolution). Hint names are prefixed with source file name to avoid collisions (e.g., `api_IMyInterface.g.cs`). Duplicate symbol names across files produce a diagnostic warning.
- `context.AnalyzerConfigOptionsProvider.GlobalOptions` → read MSBuild properties:
  - `build_property.DtsSharp_RootNamespace` → `EmitterOptions.RootNamespace`
  - `build_property.DtsSharp_ConverterType` → `EmitterOptions.InterfaceConverterTypeName`
  - `build_property.DtsSharp_DocLinkBaseUrl` → `EmitterOptions.DocLinkBaseUrl`
  - `build_property.DtsSharp_OutputPathPrefix` → `EmitterOptions.OutputPathPrefix`
  - `build_property.DtsSharp_IgnoreFile` → path pattern to match against `AdditionalTexts` to identify the ignore file
- Combine `.d.ts` content + options → parse → model → emit → `context.AddSource(hintName, sourceText)`
- One `AddSource` call per generated file (matching emitter's per-file output)

**Note:** This task depends on task 4 (parser core grammar). The generator wires the full pipeline: parser output → emitter → AddSource. The parser must be complete before this task can produce meaningful output.

**Exclusion support (all inputs via AdditionalTexts):**
- The ignore file MUST be included as an `<AdditionalFiles>` item by the consumer, NOT read from filesystem
- `DtsSharp_IgnoreFile` MSBuild property provides the path pattern to identify which additional file is the ignore file
- This ensures Roslyn tracks the ignore file as an incremental input — filesystem reads would cause stale generation
- Load ignore file content from `AdditionalTextsProvider`, pass to emitter as `IgnoreList`

**Incremental caching (structural equality):**
- **Replace `List<T>` with `ImmutableArray<T>`** in all model types — `List<T>` has reference equality which breaks Roslyn's incremental comparison
- Model types must implement structural deep equality (`IEquatable<T>`) or use records with custom collection equality
- Consider using a content hash of the `.d.ts` text as the pipeline boundary value (simple, correct, avoids deep model comparison)
- **`IsExternalInit` polyfill**: required only if using C# records on `netstandard2.0`. If using manual `IEquatable<T>` instead, the polyfill is not needed.
- **`System.Collections.Immutable`** NuGet package required for `ImmutableArray<T>` on ns2.0

**NuGet packaging:**
- Set in `.csproj`: `<IsRoslynComponent>true</IsRoslynComponent>` or manually configure `analyzers/dotnet/cs/` pack layout
- `DtsSharp.Runtime` should be a package dependency that consumers automatically get
- Reference pattern: [Mapperly](https://github.com/riok/mapperly) for production source generator packaging

## Key context

- The emitter (after task 2) returns `IReadOnlyList<(string hintName, string source)>` — the generator just iterates and calls `context.AddSource()`.
- Roslyn incremental generators must be deterministic and side-effect-free.
- All generator inputs (`.d.ts` files AND ignore file) must come through `AdditionalTextsProvider` for correct incremental tracking.
- The simplest correct caching approach may be: hash `.d.ts` content + serialized options as the pipeline key, and if unchanged, skip re-emission entirely.

## Acceptance
- [ ] `DtsSharpGenerator` implements `IIncrementalGenerator`
- [ ] Reads `.d.ts` files from `AdditionalTextsProvider` — multiple files processed independently
- [ ] Hint names prefixed with source file name to avoid collisions
- [ ] Duplicate symbol diagnostics reported when multiple files generate conflicting types
- [ ] Reads MSBuild properties from `AnalyzerConfigOptionsProvider.GlobalOptions`
- [ ] Ignore file loaded via `AdditionalTextsProvider` (NOT filesystem) for proper incremental tracking
- [ ] `DtsSharp_IgnoreFile` property identifies which additional file is the ignore file
- [ ] Pipeline: parse → model → emit → `context.AddSource()`
- [ ] Model types use `ImmutableArray<T>` (not `List<T>`) for collections
- [ ] Structural equality implemented — unchanged `.d.ts` skips re-emission
- [ ] `IsExternalInit` polyfill included if records are used (conditional)
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
