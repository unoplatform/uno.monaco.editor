# fn-11-extract-emitter-as-general-purpose-dts.3 Create runtime companion package and CLI dotnet tool

## Description
Create two additional projects: a runtime companion NuGet package containing `InterfaceToClassConverter`, and a CLI tool that wraps the core library as a `dotnet tool`. The CLI owns all file I/O concerns including ignore-file loading and output directory management (coupling point #6 from the original emitter).

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp.Runtime/DtsSharp.Runtime.csproj` (new)
- `tools/DtsSharp/DtsSharp.Runtime/InterfaceToClassConverter.cs` (extracted from `MonacoEditorComponent/Helpers/InterfaceToClassConverter.cs`)
- `tools/DtsSharp/DtsSharp.Cli/DtsSharp.Cli.csproj` (new — `<PackAsTool>true</PackAsTool>`)
- `tools/DtsSharp/DtsSharp.Cli/Program.cs` (new — CLI wrapper owning file I/O)
- `tools/DtsSharp/DtsSharp.slnx` (update — add new projects)

## Approach

**Runtime package:**
- Extract `InterfaceToClassConverter<TInterface, TClass>` from `MonacoEditorComponent/Helpers/InterfaceToClassConverter.cs` into `DtsSharp.Runtime` namespace
- ~25 lines — generic STJ `JsonConverter` that reads JSON as the concrete class and exposes it as the interface
- Target `netstandard2.0` for maximum consumer compatibility

**CLI tool:**
- Owns all file I/O: input loading, output directory creation, ignore-file loading
- **Coupling point #6**: No `FindToolDirectory` auto-discovery. Ignore file is always an explicit `--ignore-file` parameter.
- Dual input mode: `--input <file>` accepts either `.d.ts` or `.json` (detect by extension)
- For `.d.ts` input: stub with `throw new NotImplementedException("Parser not yet available")` until task 7 wires it
- For `.json` input: deserialize directly (existing behavior)
- `<PackAsTool>true</PackAsTool>`, `<ToolCommandName>dts-sharp</ToolCommandName>`
- CLI flags: `--input`, `--output`, `--ignore-file`, `--root-namespace`, `--converter-type`, `--no-docs`
- CLI constructs `EmitterOptions` from flags and passes to library

## Key context

- `InterfaceToClassConverter` deliberately serializes the concrete type, not the interface. This is correct for proxy patterns.
- Use raw arg parsing (match current `MonacoTypeEmitter/Program.cs` approach, keep it simple).
- `docfx` is a reference for `PackAsTool` packaging.

## Acceptance
- [ ] `DtsSharp.Runtime` project compiles targeting `netstandard2.0`
- [ ] `InterfaceToClassConverter<TInterface, TClass>` works identically to the Monaco version
- [ ] `DtsSharp.Cli` compiles and runs: `dotnet run --project tools/DtsSharp/DtsSharp.Cli -- --help` shows usage
- [ ] CLI accepts `--input model.json --output ./out/` and produces C# files
- [ ] CLI accepts `--input api.d.ts` and shows clear error that parser is pending
- [ ] CLI flag `--ignore-file` loads ignore list (no auto-discovery)
- [ ] CLI constructs `EmitterOptions` from flags and passes to library
- [ ] `<PackAsTool>true</PackAsTool>` set; `dotnet pack` produces a tool NuGet package
- [ ] All projects referenced in `DtsSharp.slnx`

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
