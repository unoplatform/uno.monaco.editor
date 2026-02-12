## Description
Major rewrite of `README.md` following NuGet README best practices and Uno Platform documentation standards. This task absorbs the README update scope from fn-4.7.

**Size:** M
**Files:** `README.md`, `MonacoEditorComponent/MonacoEditorComponent.csproj` (PackageReadmeFile)

## Approach

Follow the NuGet README standard structure:

1. **Header**: Package name (`Uno.Monaco.Editor`), one-sentence description, badges (NuGet version, build status, code coverage)
2. **Key Features**: Bulleted list (IntelliSense, syntax highlighting, themes, decorations, markers, language providers, dual-platform support)
3. **Platform Support Matrix**: Table showing feature support across browserwasm, desktop (Windows/macOS/Linux). Note platform-asymmetric APIs.
4. **Getting Started**: Quick install (`dotnet add package Uno.Monaco.Editor`), prerequisites (.NET 10 SDK, wasm-tools workload for WASM), minimal XAML + C# example
5. **Usage Overview**: Link to detailed docs (`docs/architecture.md`, getting started guide, API cookbook)
6. **Build from Source**: `install-dependencies.ps1`, `dotnet restore`, `dotnet build` for both targets
7. **Monaco Version**: **Verify from root `package.json` and `node_modules/monaco-editor/package.json`**
8. **Breaking Changes**: Link to CHANGELOG.md for migration from 2.0.0-dev.60
9. **Contributing**: Link to AGENTS.md conventions
10. **License**: MIT

Remove: "early alpha state" language, stale API surface listings, wrong NuGet package name.

Add `<PackageReadmeFile>README.md</PackageReadmeFile>` to `MonacoEditorComponent.csproj` if not already present.

## Key context

- Current README is 92 lines, outdated
- Package ID changed from `Monaco.Editor` to `Uno.Monaco.Editor` (csproj line 15)
- Follow Uno Platform "Uno-only feature template" pattern
- Depends on architecture docs (fn-5.3) and CHANGELOG (fn-5.2) for linking

## Acceptance
- [ ] README follows NuGet README standard structure
- [ ] "Early alpha" language removed
- [ ] Package name corrected to `Uno.Monaco.Editor` throughout
- [ ] Platform support matrix table (browserwasm, desktop Windows/macOS/Linux)
- [ ] Getting started section with `dotnet add package` and minimal XAML/C# example
- [ ] Build-from-source instructions for both targets
- [ ] Monaco Editor version verified from `package.json` and noted correctly
- [ ] Links to architecture docs, CHANGELOG, getting started guide
- [ ] `<PackageReadmeFile>README.md</PackageReadmeFile>` present in `MonacoEditorComponent.csproj`
- [ ] No stale/outdated API surface info
- [ ] fn-4.7 README scope covered (build instructions for .NET 10, dual targets, no VS 2019/Legacy Edge)
