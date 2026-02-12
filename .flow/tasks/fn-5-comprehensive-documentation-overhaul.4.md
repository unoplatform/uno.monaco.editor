# fn-5-comprehensive-documentation-overhaul.4 Major README rewrite with platform matrix and getting started

## Description
Major rewrite of `README.md` following NuGet README best practices and Uno Platform documentation standards. This task absorbs the README update scope from fn-4.7 (CI modernization cleanup).

**Size:** M
**Files:** `README.md`

## Approach

Follow the NuGet README standard structure (from Microsoft guidance) adapted for Uno Platform:

1. **Header**: Package name (`Uno.Monaco.Editor`), one-sentence description, badges (NuGet version, build status, code coverage)
2. **Key Features**: Bulleted list of capabilities (IntelliSense, syntax highlighting, themes, decorations, markers, language providers, dual-platform support)
3. **Platform Support Matrix**: Table showing feature support across browserwasm, desktop (Windows/macOS/Linux)
   - Note platform-asymmetric APIs (e.g., `AddActionAsync` desktop-unsupported)
4. **Getting Started**: Quick install (`dotnet add package Uno.Monaco.Editor`), prerequisites (.NET 10 SDK, wasm-tools workload for WASM), minimal XAML + C# example
5. **Usage Overview**: Link to detailed docs (`docs/architecture.md`, getting started guide, API cookbook)
6. **Build from Source**: `install-dependencies.ps1`, `dotnet restore`, `dotnet build` for both targets (browserwasm, desktop)
7. **Monaco Version**: **Verify from `MonacoEditorComponent/monaco-editor/package.json`** — do not hard-code
8. **Breaking Changes**: Link to CHANGELOG.md for migration from 2.0.0-dev.60
9. **Contributing**: Link to AGENTS.md conventions
10. **License**: MIT (per existing)

Remove outdated content:
- Remove "early alpha state" language (line 7 of current README)
- Fix NuGet package name: `Monaco.Editor` → `Uno.Monaco.Editor`
- Update build notes for .NET 10 + dual targets
- Remove stale API surface listings that don't match current code

## Key context

- Current README is 92 lines, last substantially updated pre-refactoring
- NuGet package ID changed from `Monaco.Editor` to `Uno.Monaco.Editor` (csproj line 15)
- Package should include README via `<PackageReadmeFile>README.md</PackageReadmeFile>` in csproj
- Follow Uno Platform "Uno-only feature template" pattern: intro → usage → platform matrix → working code
- Depends on architecture docs (fn-5.3) and CHANGELOG (fn-5.2) existing so we can link to them
- `install-dependencies.ps1` is the prerequisite step (downloads Monaco distribution)
## Approach

Follow the NuGet README standard structure (from Microsoft guidance) adapted for Uno Platform:

1. **Header**: Package name (`Uno.Monaco.Editor`), one-sentence description, badges (NuGet version, build status, code coverage)
2. **Key Features**: Bulleted list of capabilities (IntelliSense, syntax highlighting, themes, decorations, markers, language providers, dual-platform support)
3. **Platform Support Matrix**: Table showing feature support across browserwasm, desktop (Windows/macOS/Linux)
   - Note platform-asymmetric APIs (e.g., `AddActionAsync` desktop-unsupported)
4. **Getting Started**: Quick install (`dotnet add package Uno.Monaco.Editor`), prerequisites (.NET 10 SDK, wasm-tools workload for WASM), minimal XAML + C# example
5. **Usage Overview**: Link to detailed docs (`docs/architecture.md`, getting started guide, API cookbook)
6. **Build from Source**: `install-dependencies.ps1`, `dotnet restore`, `dotnet build` for both targets (browserwasm, desktop)
7. **Monaco Version**: Note that the package ships Monaco Editor 0.54.0
8. **Breaking Changes**: Link to CHANGELOG.md for migration from 2.0.0-dev.60
9. **Contributing**: Link to AGENTS.md conventions
10. **License**: MIT (per existing)

Remove outdated content:
- Remove "early alpha state" language (line 7 of current README)
- Fix NuGet package name: `Monaco.Editor` → `Uno.Monaco.Editor`
- Update build notes for .NET 10 + dual targets
- Remove stale API surface listings that don't match current code

## Key context

- Current README is 92 lines, last substantially updated pre-refactoring
- NuGet package ID changed from `Monaco.Editor` to `Uno.Monaco.Editor` (csproj line 15)
- Package should include README via `<PackageReadmeFile>README.md</PackageReadmeFile>` in csproj
- Follow Uno Platform "Uno-only feature template" pattern: intro → usage → platform matrix → working code
- Depends on architecture docs (fn-5.3) existing so we can link to them
- `install-dependencies.ps1` is the prerequisite step (downloads Monaco distribution)
## Acceptance
- [ ] README follows NuGet README standard structure (name, badges, features, install, usage, build)
- [ ] "Early alpha" language removed
- [ ] Package name corrected to `Uno.Monaco.Editor` throughout
- [ ] Platform support matrix table (browserwasm, desktop Windows/macOS/Linux)
- [ ] Getting started section with `dotnet add package` and minimal XAML/C# example
- [ ] Build-from-source instructions for both targets (browserwasm + desktop)
- [ ] Monaco Editor version verified from `package.json` and noted correctly
- [ ] Links to architecture docs, CHANGELOG, getting started guide
- [ ] `<PackageReadmeFile>README.md</PackageReadmeFile>` added to csproj (or verified present)
- [ ] No stale/outdated API surface info
- [ ] fn-4.7 README scope covered (build instructions for .NET 10, dual targets, no VS 2019/Legacy Edge references)
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
