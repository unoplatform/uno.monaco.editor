# Modernize CI, Add Code Coverage, and Replace Broken Type Generator

## Overview

Three workstreams:
1. **CI modernization**: Update runner images (macos-26), bump stale action versions, delete legacy `build/` directory, update README
2. **Code coverage**: Add full MonacoEditorComponent coverage collection across all CI test jobs (ubuntu unit+WASM, Windows DesktopCDP, macOS WASM) with merged reporting, following the Humanizer pattern
3. **Type generator replacement**: Replace the broken PowerShell/TypedocConverter pipeline with a ts-morph parser + .NET CLI emitter, with tests

**Why the generator must be replaced:** The current `GenerateMonacoTypings/` pipeline uses TypeDoc 0.20.37 + TypeScript 4.2.4 + TypedocConverter (unmaintained since 2023). Monaco 0.54.0 uses TS 5.x features that break TypeDoc parsing. The pipeline is silently broken — nothing tests whether it can parse current `monaco.d.ts`. The `SerializationContractTests.cs` tests the output types but not the generator pipeline itself.

**Architecture for new generator:**
```
monaco.d.ts
    │  (ts-morph, Node.js — via tsx or compiled JS)
    ▼
intermediate-model.json   ← versioned schema (schemaVersion field)
    │  (.NET CLI tool)
    ▼
*.cs files                ← STJ-attributed, deterministic ordering
```

**Execution:** `npx tsx src/index.ts` or `npm run build && node dist/index.js` (defined once, used everywhere).

## Scope

**In scope:**
- Update `macos-15` → `macos-26` runner in ci.yml
- Update stale action versions in `tag-release/action.yml` and `nuget-uno-publish/action.yml`
- Delete entire `build/` directory (4 legacy files)
- Fix stale "UWP" signing label; update README.md
- Add full MonacoEditorComponent code coverage across all CI test jobs with merged reporting (Microsoft.Testing.Extensions.CodeCoverage + ReportGenerator)
- Build ts-morph type extractor with versioned intermediate JSON schema
- Build .NET CLI tool for C# emission (absorbs postprocess-stj.ps1 logic)
- Add generator tests (snapshot + smoke + round-trip with serialization validation)
- Migrate off old `GenerateMonacoTypings/` scripts and clean up all references

**Out of scope:**
- `wasm-tools-net9` workload removal (owned by fn-3.3)
- `copilot-setup-steps.yml` changes
- `install-dependencies.ps1` (confirmed active)

## Task dependency graph

```
.1 (CI runners)  .2 (build/ + README)  .4 (ts-morph parser)
       │                                        │
       ▼                                        ▼
.3 (coverage)                            .5 (.NET CLI emitter)
                                                │
                                                ▼
                                   .6 (generator tests)
                                                │
                                                ▼
                                   .7 (migration + cleanup)
```

- Tasks .1, .2, .4 are independent (parallelizable)
- Task .3 depends on .1 (both edit ci.yml)
- Task .5 depends on .4 (needs intermediate JSON schema)
- Task .6 depends on .4 + .5 (needs both tools to test)
- Task .7 depends on .4 + .5 + .6 (needs tests green before deleting old scripts)

## Quick commands

```bash
dotnet build MonacoEditorComponent.slnx --no-restore
dotnet test MonacoEditorComponent.Tests --coverage --results-directory ./TestResults

# Type extractor (after task .4)
npx tsx tools/monaco-type-extractor/src/index.ts -- node_modules/monaco-editor/monaco.d.ts -o tools/monaco-type-extractor/output/model.json

# .NET emitter (after task .5)
dotnet run --project tools/MonacoTypeEmitter -- --input tools/monaco-type-extractor/output/model.json --output MonacoEditorComponent/Monaco/
```

## Acceptance

**CI modernization (Tasks .1, .2):**
- [ ] `build-macos` uses `runs-on: macos-26`; stale action versions bumped; signing label fixed (Task .1)
- [ ] `build/` deleted; README modernized (.NET 10, browserwasm + desktop, no VS 2019/Legacy Edge) (Task .2)

**Code coverage (Task .3):**
- [ ] Coverage collected via `--coverage` in all CI test jobs: `build` (ubuntu: unit + WASM), `desktop-tests` (Windows: DesktopCDP), `build-macos` (macOS: WASM) (Task .3)
- [ ] Coverage artifacts uploaded per-job, then merged via a `coverage-report` job into a single combined report (Task .3)
- [ ] Combined Cobertura XML + HTML report uploaded as `coverage-report` artifact (Task .3)

**Type generator (Tasks .4, .5, .6, .7):**
- [ ] ts-morph extractor parses current `monaco.d.ts` (TS 5.x) → versioned intermediate JSON with deterministic ordering (Task .4)
- [ ] .NET CLI tool emits C# matching all existing patterns (string enums, numeric enums, models, interfaces, constructors/defaults, global CamelCase policy) (Task .5)
- [ ] Snapshot tests lock down emitter output; smoke test compiles emitted files and runs serialization contract subset; deterministic ordering guaranteed (Task .6)
- [ ] `GenerateMonacoTypings/` deleted; `.generator-ignore` migrated to path-based matching; no `GenerateMonacoTypings`/`TypedocConverter` references in repo (excluding .flow history) (Task .7)

## References

- [ts-morph](https://github.com/dsherret/ts-morph) (5.9k stars)
- [Humanizer CI/coverage](https://github.com/Humanizr/Humanizer)
- [Verify](https://github.com/VerifyTests/Verify) (3.4k stars)
- [macOS 26 beta — actions/runner-images#13008](https://github.com/actions/runner-images/issues/13008)
- Existing patterns: `MonacoEditorComponent/Monaco/Editor/*.cs`, `SerializationContractTests.cs`

## Risks

- **macOS 26 beta**: Rollback to `macos-15` if runner-image failures for 3+ consecutive runs.
- **Intermediate JSON schema design**: Versioned schema (`schemaVersion`) enables iterative refinement. Snapshot tests catch regressions.
- **Coverage merge across OS runners**: Each job uploads its own coverage artifact; a downstream `coverage-report` job merges them via ReportGenerator. If a job is skipped/fails, partial coverage still works.
- **Snapshot churn**: Mitigated by requiring deterministic ordering in both JSON model and emitted files.
