# fn-11-extract-emitter-as-general-purpose-dts.3 Create runtime companion package and CLI dotnet tool

## Description
Create two additional projects: a runtime companion NuGet package containing `InterfaceToClassConverter` (and any other runtime helpers emitted code depends on), and a CLI tool that wraps the core library as a `dotnet tool`.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp.Runtime/DtsSharp.Runtime.csproj` (new)
- `tools/DtsSharp/DtsSharp.Runtime/InterfaceToClassConverter.cs` (extracted from `MonacoEditorComponent/Helpers/InterfaceToClassConverter.cs`)
- `tools/DtsSharp/DtsSharp.Cli/DtsSharp.Cli.csproj` (new — `<PackAsTool>true</PackAsTool>`)
- `tools/DtsSharp/DtsSharp.Cli/Program.cs` (new — thin CLI wrapper)
- `tools/DtsSharp/DtsSharp.slnx` (update — add new projects)

## Approach

**Runtime package:**
- Extract `InterfaceToClassConverter<TInterface, TClass>` from `MonacoEditorComponent/Helpers/InterfaceToClassConverter.cs` into `DtsSharp.Runtime` namespace
- The converter is ~25 lines — a generic STJ `JsonConverter` that reads JSON as the concrete class and exposes it as the interface
- Target `netstandard2.0` for maximum consumer compatibility
- Add `<IsPackable>true</IsPackable>`, `<PackageId>DtsSharp.Runtime</PackageId>`

**CLI tool:**
- Follow the pattern from `tools/MonacoTypeEmitter/Program.cs` — parse args, load model, run emitter, write files
- Add dual input mode: `--input <file>` accepts either `.d.ts` or `.json` (detect by extension)
- For `.d.ts` input: use the parser from task 4 (stub with `throw new NotImplementedException("Parser not yet available")` until task 4 completes)
- For `.json` input: deserialize directly (existing behavior)
- `<PackAsTool>true</PackAsTool>`, `<ToolCommandName>dts-sharp</ToolCommandName>`
- CLI flags: `--input`, `--output`, `--ignore`, `--root-namespace`, `--converter-type`, `--no-docs`

## Key context

- `InterfaceToClassConverter` uses `JsonSerializer.Serialize(writer, value, typeof(TClass), options)` — this deliberately serializes the concrete type, not the interface. This is correct for proxy patterns.
- The CLI should use `System.CommandLine` if it's already a dependency, otherwise raw arg parsing (keep it simple).
- `docfx` is a good reference for `PackAsTool` packaging: `src/docfx/docfx.csproj`
## Acceptance
- [ ] `DtsSharp.Runtime` project compiles targeting `netstandard2.0`
- [ ] `InterfaceToClassConverter<TInterface, TClass>` works identically to the Monaco version
- [ ] `DtsSharp.Cli` compiles and runs: `dotnet run --project tools/DtsSharp/DtsSharp.Cli -- --help` shows usage
- [ ] CLI accepts `--input model.json --output ./out/` and produces C# files
- [ ] CLI accepts `--input api.d.ts` and shows clear error that parser is pending (until task 4)
- [ ] `<PackAsTool>true</PackAsTool>` set; `dotnet pack` produces a tool NuGet package
- [ ] All three projects referenced in `DtsSharp.slnx`
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
