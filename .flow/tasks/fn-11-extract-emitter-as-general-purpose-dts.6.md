# fn-11-extract-emitter-as-general-purpose-dts.6 Migrate uno.monaco.editor to consume source generator

## Description
Migrate `uno.monaco.editor` to use the `DtsSharp` source generator. Add project reference + AdditionalFiles, configure MSBuild properties for Monaco-specific options, verify byte-for-byte identical output, remove old `MonacoTypeEmitter` tool, update all docs.

**Size:** M
**Files:**
- `MonacoEditorComponent/MonacoEditorComponent.csproj` (modify — add DtsSharp project reference + AdditionalFiles)
- `tools/DtsSharp/monaco.generator-ignore` (moved from `tools/MonacoTypeEmitter/.generator-ignore`)
- `MonacoEditorComponent.slnx` (update — add DtsSharp projects, remove MonacoTypeEmitter)
- `AGENTS.md` (update — new tool paths)
- `README.md` (update)
- `docs/generated-type-docs-strategy.md` (update)
- `tools/MonacoTypeEmitter/` (remove after verification)

## Approach

- Add DtsSharp as a project reference (analyzer) in `MonacoEditorComponent.csproj`:
  ```xml
  <ProjectReference Include="..\tools\DtsSharp\DtsSharp\DtsSharp.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  <ProjectReference Include="..\tools\DtsSharp\DtsSharp.Runtime\DtsSharp.Runtime.csproj" />
  ```
- Add `<AdditionalFiles Include="path/to/monaco.d.ts" />` pointing to the vendored Monaco declaration file
- Set MSBuild properties for Monaco-specific configuration:
  ```xml
  <PropertyGroup>
    <DtsSharp_RootNamespace>monaco</DtsSharp_RootNamespace>
    <DtsSharp_ConverterType>Monaco.Helpers.InterfaceToClassConverter</DtsSharp_ConverterType>
    <DtsSharp_OutputPathPrefix>MonacoEditorComponent/Monaco/</DtsSharp_OutputPathPrefix>
    <DtsSharp_IgnoreFile>$(MSBuildThisFileDirectory)..\tools\DtsSharp\monaco.generator-ignore</DtsSharp_IgnoreFile>
  </PropertyGroup>
  ```
- Move `.generator-ignore` to `tools/DtsSharp/monaco.generator-ignore` (stable path)

**Byte-for-byte verification (before removing generated files):**
- Run source generator via `CSharpGeneratorDriver` test harness
- Compare output against current `MonacoEditorComponent/Monaco/` files
- Task 7 must have already achieved zero diffs
- **Generated file manifest:** Create an allowlist of files that are generator output (vs hand-authored). Only remove files on the manifest. Hand-authored files like `InterfaceToClassConverter.cs` and other Monaco helpers must be preserved.

**After verification:**
- Remove old generated `.cs` files from `MonacoEditorComponent/Monaco/` (only those on the manifest)
- Keep `MonacoEditorComponent/Helpers/InterfaceToClassConverter.cs` with `Monaco.Helpers` namespace (local copy stays; DtsSharp.Runtime used by third-party consumers)
- Remove `tools/MonacoTypeEmitter/` after verification
- Update AGENTS.md, README.md, docs to reflect new source generator workflow

## Key context

- With source generator, generated types are no longer checked into the repo — they're produced at compile time
- AGENTS.md lines 35, 44-46, 73-75 reference old paths
- The `MonacoEditorComponent/Monaco/` directory contains both generated AND hand-authored files — removal must be selective via manifest
- ts-morph extractor can be kept as reference or removed (separate decision)

## Acceptance
- [ ] DtsSharp source generator produces byte-for-byte identical output to old MonacoTypeEmitter (verified by diff test before migration)
- [ ] Generated file manifest created — only manifested files removed
- [ ] Hand-authored files in `MonacoEditorComponent/Monaco/` preserved
- [ ] `dotnet build MonacoEditorComponent.slnx` succeeds with source-generated types
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm` succeeds
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop` succeeds
- [ ] `DtsSharp_IgnoreFile` property filters excluded types correctly
- [ ] Old generated `.cs` files removed from `MonacoEditorComponent/Monaco/` (manifested only)
- [ ] `tools/MonacoTypeEmitter/` removed
- [ ] AGENTS.md updated: new source generator workflow
- [ ] docs/generated-type-docs-strategy.md updated
- [ ] `tools/DtsSharp/monaco.generator-ignore` exists (moved from old location)

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
