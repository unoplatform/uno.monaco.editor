# fn-4-modernize-ci-add-code-coverage-and.7 Migrate off old GenerateMonacoTypings and clean up

## Description
Migrate from old GenerateMonacoTypings/ pipeline to new tools, delete old scripts, and clean up all references.

**Size:** S
**Files:** `GenerateMonacoTypings/` (delete all), `AGENTS.md`, `.gitignore`, `.gitattributes`

> **Note:** The README.md update scope originally in this task has been absorbed into fn-5-comprehensive-documentation-overhaul.4 (major README rewrite).

**Depends on:** Tasks .4, .5, .6 (new tools working + tests green)

## Approach

1. Run new generator against current `monaco.d.ts`, diff output against existing `MonacoEditorComponent/Monaco/` (excluding ignored files)
2. Resolve any differences
3. Delete `GenerateMonacoTypings/` (all: generate-typings.ps1, postprocess-stj.ps1, package.json, README.md, .generator-ignore — note: `output/` does not exist on disk, it is gitignored)
4. Update AGENTS.md: "Key Directories" section → reference `tools/monaco-type-extractor/` and `tools/MonacoTypeEmitter/`
5. ~~Update README.md: reference new tools~~ *(absorbed into fn-5.4)*
6. Update `.gitignore` (lines 289-291: comment + `GenerateMonacoTypings/output/` and `GenerateMonacoTypings/.temp/`) and `.gitattributes` (line 68: comment referencing `GenerateMonacoTypings/.generator-ignore`): remove old paths, add new. Note: preserve nearby entries added by task .6 (lines 293-294: `*.received.cs` snapshot test pattern)
<!-- Updated by plan-sync: fn-4.6 shifted .gitignore line numbers from 287-288 to 289-291 and added *.received.cs at 293-294 -->
7. Verify: `grep -rn "GenerateMonacoTypings\|TypedocConverter" . --include="*.md" --include="*.yml" --include="*.cs" --include="*.ps1" --include="*.json" | grep -v ".flow/"` returns no results
## Acceptance
- [ ] `GenerateMonacoTypings/` directory fully deleted
- [ ] No `GenerateMonacoTypings` or `TypedocConverter` references in repo (excluding `.flow/` history docs) — verified by grep
- [ ] AGENTS.md references new `tools/` directories
- [ ] ~~README.md references new generator tool~~ *(absorbed into fn-5.4)*
- [ ] `.gitignore` and `.gitattributes` updated (old paths removed, new paths added)
- [ ] All tests still pass
## Done summary
Migrated off old GenerateMonacoTypings pipeline: deleted the directory and all 5 files, updated AGENTS.md/.gitignore/.gitattributes/install-dependencies.ps1 to reference new tools/ directory, verified no stale references remain. README.md scope absorbed into fn-5.4.
## Evidence
- Commits: e2fe5cc e2fe5cc
- Tests: dotnet build (0 errors), dotnet test MonacoTypeEmitter.Tests (19 passed), grep verification (no stale refs) dotnet build MonacoEditorComponent.slnx --no-restore, dotnet test --project tools/MonacoTypeEmitter.Tests/MonacoTypeEmitter.Tests.csproj (19 passed), grep -rn GenerateMonacoTypings|TypedocConverter (no results)
- PRs: N/A