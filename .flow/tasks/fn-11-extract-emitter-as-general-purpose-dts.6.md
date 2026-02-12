# fn-11-extract-emitter-as-general-purpose-dts.6 Migrate uno.monaco.editor to consume extracted library

## Description
Migrate `uno.monaco.editor` to use the extracted `DtsSharp` library. Move the ignore file to a stable path under `tools/DtsSharp/`, update all docs/scripts, verify byte-for-byte parity (task 7 resolves all diffs first).

**Size:** M
**Files:**
- `tools/DtsSharp/monaco.generator-ignore` (moved from `tools/MonacoTypeEmitter/.generator-ignore`)
- `MonacoEditorComponent.slnx` (update)
- `MonacoEditorComponent/Helpers/InterfaceToClassConverter.cs` (keep local copy)
- `AGENTS.md` (update)
- `README.md` (update)
- `docs/generated-type-docs-strategy.md` (update)
- `tools/MonacoTypeEmitter/` (remove after verification)

## Approach

- Move `.generator-ignore` to `tools/DtsSharp/monaco.generator-ignore` (stable path that survives MonacoTypeEmitter removal)
- Create Monaco-specific `EmitterOptions`: `OutputPathPrefix`, `InterfaceConverterTypeName`, Monaco `IDocLinkProvider`
- Run new CLI → diff against current output → must be byte-for-byte identical (task 7 already resolved all parser diffs)
- Keep `MonacoEditorComponent/Helpers/InterfaceToClassConverter.cs` with `Monaco.Helpers` namespace
- Remove `tools/MonacoTypeEmitter/` after verification
- Update all docs to new paths and `--ignore-file` flag

## Key context

- AGENTS.md lines 35, 44-46, 73-75 reference old paths
- ts-morph extractor can be kept as reference or removed

## Acceptance
- [ ] `tools/DtsSharp/monaco.generator-ignore` exists (moved from old location)
- [ ] `dotnet run --project tools/DtsSharp/DtsSharp.Cli -- --input node_modules/monaco-editor/monaco.d.ts --output MonacoEditorComponent/Monaco/ --ignore-file tools/DtsSharp/monaco.generator-ignore` produces byte-for-byte identical output
- [ ] `dotnet build MonacoEditorComponent.slnx` succeeds
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm` succeeds
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop` succeeds
- [ ] AGENTS.md updated: new tool paths and `--ignore-file` flag
- [ ] docs/generated-type-docs-strategy.md updated
- [ ] `tools/MonacoTypeEmitter/` removed
- [ ] All docs use `--ignore-file` consistently

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
