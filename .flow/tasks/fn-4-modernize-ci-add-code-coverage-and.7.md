# fn-4-modernize-ci-add-code-coverage-and.7 Migrate off old GenerateMonacoTypings and clean up

## Description
Migrate from old GenerateMonacoTypings/ pipeline to new tools, delete old scripts, and clean up all references.

**Size:** S
**Files:** `GenerateMonacoTypings/` (delete all), `AGENTS.md`, `README.md`, `.gitignore`, `.gitattributes`

**Depends on:** Tasks .4, .5, .6 (new tools working + tests green)

## Approach

1. Run new generator against current `monaco.d.ts`, diff output against existing `MonacoEditorComponent/Monaco/` (excluding ignored files)
2. Resolve any differences
3. Delete `GenerateMonacoTypings/` (all: generate-typings.ps1, postprocess-stj.ps1, package.json, README.md, .generator-ignore, output/)
4. Update AGENTS.md: "Key Directories" section → reference `tools/monaco-type-extractor/` and `tools/MonacoTypeEmitter/`
5. Update README.md: reference new tools
6. Update `.gitignore` (lines 287-288) and `.gitattributes` (line 68): remove old paths, add new
7. Verify: `grep -rn "GenerateMonacoTypings\|TypedocConverter" . --include="*.md" --include="*.yml" --include="*.cs" --include="*.ps1" --include="*.json" | grep -v ".flow/"` returns no results
## Acceptance
- [ ] `GenerateMonacoTypings/` directory fully deleted
- [ ] No `GenerateMonacoTypings` or `TypedocConverter` references in repo (excluding `.flow/` history docs) — verified by grep
- [ ] AGENTS.md references new `tools/` directories
- [ ] README.md references new generator tool
- [ ] `.gitignore` and `.gitattributes` updated (old paths removed, new paths added)
- [ ] All tests still pass
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
