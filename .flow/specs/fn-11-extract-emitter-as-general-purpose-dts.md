# General-Purpose .d.ts → C# Roslyn Source Generator

## Overview

Extract the Monaco type emitter into a standalone Roslyn incremental source generator that converts TypeScript `.d.ts` declaration files into C# proxy types at compile time. Pure .NET, no Node.js, no CLI — consumers just add a NuGet package and their `.d.ts` file.

### Consumer experience

```xml
<PackageReference Include="DtsSharp" />
<AdditionalFiles Include="api.d.ts" />
```

At build time, the source generator reads the `.d.ts`, parses it in C#, and emits typed C# proxies directly into the compilation. No separate build step.

### Current state

The existing pipeline is two stages: ts-morph (Node.js) parses `.d.ts` → JSON, then a .NET CLI emitter reads JSON → emits C# files. The emitter is ~90% generic already with only ~6 Monaco-specific coupling points.

### Target state

Two NuGet packages:
- **DtsSharp** — Roslyn `IIncrementalGenerator` analyzer package. Contains the .d.ts parser + C# emitter. Targets `netstandard2.0` (with potential Roslyn-version-specific builds for perf).
- **DtsSharp.Runtime** — Small runtime package with `InterfaceToClassConverter` and any other types that generated code references.

## Scope

**In scope:**
- Roslyn incremental source generator reading `.d.ts` via `AdditionalTextsProvider`
- C# declaration parser for `.d.ts` (interfaces, classes, enums, type aliases, functions, namespaces, generics, unions/intersections)
- Decoupled emitter with configurable options (via MSBuild properties or attributes)
- Runtime NuGet package for `InterfaceToClassConverter`
- Analyzer NuGet packaging (`analyzers/dotnet/cs/`)
- Migration of `uno.monaco.editor` to consume the source generator
- Test suite with non-Monaco `.d.ts` fixtures

**Out of scope:**
- CLI tool (no executable)
- JSON intermediate format for consumers
- Full TypeScript type checker
- Multi-file `.d.ts` with `/// <reference>` following (v1 = single file)
- Construct signatures (not in intermediate model)

## Dependencies

- **fn-10** — should complete first (actively improving emitter code)

## Quick commands

```bash
# Build the generator
dotnet build tools/DtsSharp/DtsSharp.slnx

# Run tests
dotnet test --project tools/DtsSharp/DtsSharp.Tests

# Consumer project (after packaging):
# Just add <AdditionalFiles Include="api.d.ts" /> and build
```

## Acceptance

- [ ] Source generator compiles targeting `netstandard2.0` with zero Monaco references
- [ ] Consumer project with `<AdditionalFiles Include="test.d.ts" />` gets generated C# types at build time
- [ ] Parser handles: interfaces, classes, enums, type aliases, functions, namespaces, generics (with defaults/constraints), unions/intersections, arrays, literals, `typeof`, `keyof`
- [ ] Parser has deterministic fallbacks for unsupported constructs
- [ ] `DtsSharp.Runtime` contains `InterfaceToClassConverter` in a generic namespace
- [ ] Configuration via MSBuild properties: root namespace, converter type name, doc link base URL
- [ ] Incremental generator caches correctly — unchanged `.d.ts` files don't trigger re-emission
- [ ] `uno.monaco.editor` produces byte-for-byte identical output using the source generator
- [ ] Test suite includes 3+ real non-Monaco library `.d.ts` fixtures
- [ ] NuGet package layout: `analyzers/dotnet/cs/` for generator, `lib/netstandard2.0/` for runtime

## Architecture

```mermaid
graph TB
    subgraph "Build Time (DtsSharp analyzer)"
        AT["AdditionalTextsProvider"] --> DTS[".d.ts content"]
        MSB["MSBuild Properties"] --> Opts["EmitterOptions"]
        DTS --> Parser["DtsParser"]
        Parser --> Model["TypeModel (internal)"]
        Model --> Emitter["CSharpEmitter"]
        Opts --> Emitter
        Emitter --> SRC["Generated C# source"]
        SRC --> AddSrc["context.AddSource()"]
    end

    subgraph "NuGet Packages"
        Gen["DtsSharp<br/>(analyzers/dotnet/cs/)"]
        RT["DtsSharp.Runtime<br/>(lib/netstandard2.0/)"]
    end

    AddSrc -.->|"generated code refs"| RT
```

## Key design decisions

1. **Roslyn incremental source generator** — runs at compile time via `IIncrementalGenerator`. Reads `.d.ts` from `AdditionalTextsProvider`. No CLI, no separate build step.

2. **netstandard2.0 baseline** — required for Roslyn host compatibility. Can add Roslyn-version-specific builds (e.g., `analyzers/roslyn4.4/dotnet/cs/`) if newer APIs offer meaningful perf gains.

3. **Incremental caching** — model types must have value equality for the incremental pipeline to skip re-emission when `.d.ts` hasn't changed. Use `record` or implement `IEquatable<T>`.

4. **Configuration via MSBuild properties** — read from `AnalyzerConfigOptionsProvider.GlobalOptions`. Properties like `build_property.DtsSharp_RootNamespace`, `build_property.DtsSharp_ConverterType`. Set in consumer's `.csproj` or `Directory.Build.props`.

5. **Single analyzer package** — parser + emitter + generator wiring all in one package. Keep it simple.

6. **Runtime companion** — `DtsSharp.Runtime` ships separately in `lib/netstandard2.0/` so generated code can reference it at runtime without pulling in the analyzer.

## References

- Current emitter: `tools/MonacoTypeEmitter/Emitter/CSharpEmitter.cs`
- Current model: `tools/MonacoTypeEmitter/Model/MonacoModel.cs`
- Packaging reference: [Mapperly](https://github.com/riok/mapperly) (production source generator packaging)
- Pattern reference: [dagger/dagger](https://github.com/dagger/dagger) (IIncrementalGenerator reading JSON via AdditionalTexts)
- Pattern reference: [spectre.console](https://github.com/spectreconsole/spectre.console) (IIncrementalGenerator reading JSON for emoji generation)
