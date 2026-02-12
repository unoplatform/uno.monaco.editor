# fn-11-extract-emitter-as-general-purpose-dts.6 Migrate uno.monaco.editor to consume extracted library

## Description
Migrate `uno.monaco.editor` to use the extracted `DtsSharp` library instead of the in-tree `MonacoTypeEmitter`. Update build scripts, documentation, and CI to reference the new tool. Verify generated output is identical.

**Size:** M
**Files:**
- `MonacoEditorComponent.slnx` (update — add DtsSharp project references or NuGet refs)
- `MonacoEditorComponent/Helpers/InterfaceToClassConverter.cs` (update — reference DtsSharp.Runtime or keep local copy)
- `AGENTS.md` (update — "Key Directories", "Development Workflow" sections)
- `README.md` (update — "Type Generation Pipeline" section if present)
- `docs/generated-type-docs-strategy.md` (update — emitter file references)
- `.github/workflows/ci.yml` (update — if CI invokes the emitter)
- `tools/MonacoTypeEmitter/` (potentially remove or archive)

## Approach

- Create a Monaco-specific `EmitterOptions` configuration that reproduces current behavior: `OutputPathPrefix = "MonacoEditorComponent/Monaco/"`, `InterfaceConverterTypeName = "Monaco.Helpers.InterfaceToClassConverter"`, Monaco `IDocLinkProvider` implementation, etc.
- Run the new CLI against `monaco.d.ts` → diff generated output against current `MonacoEditorComponent/Monaco/` files → must be byte-for-byte identical (or only whitespace/comment diffs)
- Create a `MonacoDocLinkProvider` (implements `IDocLinkProvider`) that generates `https://microsoft.github.io/monaco-editor/typedoc/` URLs — this can live in the repo as a small helper class
- Decision point: keep `tools/MonacoTypeEmitter/` as archived reference, or remove entirely. Recommend: remove after verification to avoid confusion.
- Update all documentation that references the old tool paths

## Key context

- The `.generator-ignore` file at `tools/MonacoTypeEmitter/.generator-ignore` contains 18 Monaco-specific entries. These stay in this repo (passed via `--ignore` flag to the new CLI).
- The `InterfaceToClassConverter` in `MonacoEditorComponent/Helpers/` can either: (a) stay as-is with `Monaco.Helpers` namespace, or (b) be replaced by a reference to `DtsSharp.Runtime`. Option (a) avoids changing all generated file imports. Recommend (a) for now.
- AGENTS.md lines 35, 44-46, 73-75 reference the old tool paths.
## Acceptance
- [ ] `dotnet run --project tools/DtsSharp/DtsSharp.Cli -- --input node_modules/monaco-editor/monaco.d.ts --output MonacoEditorComponent/Monaco/ --ignore tools/MonacoTypeEmitter/.generator-ignore` produces identical output to current generated files
- [ ] `dotnet build MonacoEditorComponent.slnx` succeeds after migration
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm` succeeds
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop` succeeds
- [ ] AGENTS.md updated: "Key Directories" and "Development Workflow" reference new tool paths
- [ ] docs/generated-type-docs-strategy.md updated with new emitter references
- [ ] Old `tools/MonacoTypeEmitter/` either removed or clearly marked as archived
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
