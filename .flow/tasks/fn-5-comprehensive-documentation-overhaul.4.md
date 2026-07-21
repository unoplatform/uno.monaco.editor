## Description
Major rewrite of `README.md` following NuGet README best practices and Uno Platform documentation standards. This task absorbs the README update scope from fn-4.7 and owns updating fn-4.7's spec to reflect that.

**Size:** M
**Files:** `README.md`, `MonacoEditorComponent/MonacoEditorComponent.csproj` (PackageReadmeFile + pack item), `.flow/tasks/fn-4-modernize-ci-add-code-coverage-and.7.md` (remove README scope)

## Approach

Follow the NuGet README standard structure:

1. **Header**: Package name (`Uno.Monaco.Editor`), one-sentence description, badges
2. **Key Features**: Bulleted list (IntelliSense, syntax highlighting, themes, decorations, markers, language providers, dual-platform support)
3. **Platform Support Matrix**: Table showing feature support across browserwasm, desktop (Windows/macOS/Linux). Note platform-asymmetric APIs.
4. **Getting Started**: Quick install, prerequisites (.NET 10 SDK, wasm-tools), minimal XAML + C# example
5. **Usage Overview**: Link to `docs/architecture.md`, getting started guide, API cookbook
6. **Build from Source**: `install-dependencies.ps1`, `dotnet restore`, `dotnet build` for both targets
7. **Monaco Version**: Verify from root `package.json` and `node_modules/monaco-editor/package.json`
8. **Breaking Changes**: Link to CHANGELOG.md
9. **Contributing**: Link to AGENTS.md
10. **License**: MIT

**NuGet packing setup** in `MonacoEditorComponent.csproj`:
- Add `<PackageReadmeFile>README.md</PackageReadmeFile>` if not present
- Add pack target to include repo-root README via `_PackageFiles` injection (the standard `<None Pack="true"/>` pattern does not work with Uno SDK's `GenerateLibraryLayout=true`)
- Validate with `dotnet pack MonacoEditorComponent/MonacoEditorComponent.csproj -c Release` (verify README included)

**fn-4.7 coordination**: Update `.flow/tasks/fn-4-modernize-ci-add-code-coverage-and.7.md` to remove README update scope and note it is handled by fn-5.4.

Remove: "early alpha" language, stale API listings, wrong NuGet package name.

## Key context
- Current README is 92 lines, outdated
- Package ID changed to `Uno.Monaco.Editor` (csproj line 15)
- Follow Uno Platform "Uno-only feature template" pattern
- Depends on architecture docs (fn-5.3) and CHANGELOG (fn-5.2) for linking

## Acceptance
- [ ] README follows NuGet README standard structure
- [ ] "Early alpha" language removed
- [ ] Package name corrected to `Uno.Monaco.Editor` throughout
- [ ] Platform support matrix table
- [ ] Getting started section with minimal XAML/C# example
- [ ] Build-from-source instructions for both targets
- [ ] Monaco version verified and noted correctly
- [ ] Links to architecture docs, CHANGELOG; getting started guide and API cookbook listed as coming soon (created by fn-5.7)
- [ ] `<PackageReadmeFile>README.md</PackageReadmeFile>` present in csproj
- [ ] Pack target includes repo-root README via `_PackageFiles` injection (`_IncludeReadmeInPackage` target)
- [ ] `dotnet pack MonacoEditorComponent/MonacoEditorComponent.csproj -c Release` succeeds with README in package
- [ ] fn-4.7 spec updated to remove README scope (note: "absorbed into fn-5.4")
- [ ] fn-4.7 README scope covered (build instructions for .NET 10, dual targets, no VS 2019/Legacy Edge)
## Done summary
Major README rewrite following NuGet README standards: added platform support matrix, getting started guide with XAML/C# example, build-from-source instructions for both targets, badges, and Monaco version section. Added PackageReadmeFile and _IncludeReadmeInPackage target to csproj for NuGet package inclusion. Updated fn-4.7 spec to note README scope absorbed into fn-5.4.
## Evidence
- Commits: 3080fb9, fb24cfd, de1a312
- Tests: dotnet build MonacoEditorComponent.slnx --no-restore, dotnet pack MonacoEditorComponent/MonacoEditorComponent.csproj -c Release
- PRs: