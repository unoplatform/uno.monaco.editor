# fn-2-type-generation-pipeline-and.6 Remove Newtonsoft.Json dependency and finalize build

## Description
Remove Newtonsoft.Json package dependency from the library, update build artifacts, and verify clean build across all targets. This task achieves zero-Newtonsoft in `MonacoEditorComponent/` (the library). Repo-wide cleanup (GenerateMonacoTypings/) is finalized in task 7.

**Size:** S
**Files:** Directory.Packages.props, MonacoEditorComponent/MonacoEditorComponent.csproj, MonacoEditorComponent/Properties/MonacoEditorComponent.rd.xml, changelog.md

## Approach

- Remove `<PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />` from `Directory.Packages.props:9`
- Remove `<PackageReference Include="Newtonsoft.Json" />` from `MonacoEditorComponent.csproj:43`
- Update `MonacoEditorComponent.rd.xml` to remove Newtonsoft type directives (lines 15-19) — these are no longer needed with STJ source gen
- Verify: `grep -r "Newtonsoft" MonacoEditorComponent/ --include="*.cs" --include="*.csproj" --include="*.props"` returns zero results
- Build both targets: `dotnet build MonacoEditorTestApp -f net10.0-browserwasm` and `-f net10.0-desktop`
- Update `changelog.md` with breaking change entry:
  - Newtonsoft.Json transitive dependency removed
  - `CommandHandler` now receives `JsonElement` instead of `JObject`
  - Migration guidance for consumers

## Key context

- This is a breaking change for NuGet consumers who depend on the transitive Newtonsoft.Json dependency.
- The `rd.xml` runtime directives at `MonacoEditorComponent/Properties/MonacoEditorComponent.rd.xml` explicitly reference `Newtonsoft.Json.JsonConvert` — no longer needed.
- GenerateMonacoTypings/ may still reference Newtonsoft in its output or tooling — that is task 7's responsibility.

## Acceptance
- [ ] Zero `Newtonsoft.Json` references in MonacoEditorComponent/ (library scope: *.cs, *.csproj, *.props, *.rd.xml)
- [ ] `rd.xml` updated (Newtonsoft directives removed)
- [ ] `changelog.md` documents the breaking change with migration guidance
- [ ] `dotnet build MonacoEditorTestApp -f net10.0-browserwasm` succeeds
- [ ] `dotnet build MonacoEditorTestApp -f net10.0-desktop` succeeds
- [ ] All serialization contract tests pass

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
