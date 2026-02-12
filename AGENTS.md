<!-- BEGIN FLOW-NEXT -->
## Flow-Next

This project uses Flow-Next for task tracking. Use `.flow/bin/flowctl` instead of markdown TODOs or TodoWrite.

**Quick commands:**
```bash
.flow/bin/flowctl list                # List all epics + tasks
.flow/bin/flowctl epics               # List all epics
.flow/bin/flowctl tasks --epic fn-N   # List tasks for epic
.flow/bin/flowctl ready --epic fn-N   # What's ready
.flow/bin/flowctl show fn-N.M         # View task
.flow/bin/flowctl start fn-N.M        # Claim task
.flow/bin/flowctl done fn-N.M --summary-file s.md --evidence-json e.json
```

**Rules:**
- Use `.flow/bin/flowctl` for ALL task tracking
- Do NOT create markdown TODOs or use TodoWrite
- Re-anchor (re-read spec + status) before every task

**More info:** `.flow/bin/flowctl --help` or read `.flow/usage.md`
<!-- END FLOW-NEXT -->

# AGENTS.md

This file provides guidance to AI agents working in `uno.monaco.editor`.

## Project Overview

`uno.monaco.editor` is a Uno Platform wrapper around the Monaco web editor. The repository includes:

- `MonacoEditorComponent/`: the packaged control/library (`Uno.Monaco.Editor`)
- `MonacoEditorTestApp/`: sample app used for manual validation
- `tools/`: type generation pipeline (ts-morph extractor + .NET CLI emitter)

## Key Directories

- `MonacoEditorComponent/CodeEditor/`: control behavior and interop entry points
- `MonacoEditorComponent/Monaco/`: Monaco API wrappers (many files generated from typings)
- `MonacoEditorComponent/ts-helpermethods/`: TypeScript helpers compiled into runtime assets
- `MonacoEditorComponent/monaco-editor/`: vendored Monaco distribution used by the component
- `MonacoEditorTestApp/`: functional playground for verifying behavior
- `tools/monaco-type-extractor/`: ts-morph parser that extracts Monaco API into intermediate JSON
- `tools/MonacoTypeEmitter/`: .NET CLI tool that emits C# types from intermediate JSON
- `tools/MonacoTypeEmitter.Tests/`: snapshot and round-trip tests for the emitter

## Build Setup (Required)

1. Ensure Monaco dependencies are present:
```bash
pwsh ./install-dependencies.ps1
```
2. Restore/build solution:
```bash
dotnet restore MonacoEditorComponent.slnx
dotnet build MonacoEditorComponent.slnx --no-restore
```
3. For app validation, build both primary targets:
```bash
dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm
dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop
```

CRITICAL: Do not cancel restore/build commands mid-run. Use generous timeouts for CI-like local validation.

## Development Workflow

Validation checklist after code changes:

1. `dotnet build MonacoEditorComponent.slnx --no-restore`
2. If API surface or behavior changed, build `MonacoEditorTestApp` for browserwasm and desktop targets.
3. If Monaco typings or TS helper behavior changed, run the type generation pipeline and rebuild:
   - `npx tsx tools/monaco-type-extractor/src/index.ts -- node_modules/monaco-editor/monaco.d.ts -o tools/monaco-type-extractor/output/model.json`
   - `dotnet run --project tools/MonacoTypeEmitter -- --input tools/monaco-type-extractor/output/model.json --output MonacoEditorComponent/Monaco/`

## Code Conventions

- Keep platform-specific partials in suffixed files (for example `.wasm.cs`) when applicable.
- Maintain API naming close to Monaco/TypeScript semantics while using idiomatic C#/WinRT types.
- Prefer focused changes and avoid broad reformatting in generated or vendored content.

## Commit Guidelines

MANDATORY: all commits must use Conventional Commits (semantic commits).

Format:
```text
<type>[optional scope][!]: <description>
```

Common types:

- `fix`: bug fix (PATCH)
- `feat`: new behavior or capability (MINOR)
- `docs`: documentation only
- `test`: test changes
- `refactor`: internal code change without behavior change
- `perf`: performance improvement
- `chore`: maintenance
- `ci`: CI/workflow changes
- `build`: build/dependency tooling changes

Rules:

- Use imperative mood in subject (`add`, `update`, `remove`).
- Keep subject concise (prefer <= 50 chars).
- Use `!` for breaking changes, and include details in the commit body when needed.
- Reference issues/PRs where relevant (for example: `fix: ... (fixes #123)`).

Examples:

- `fix(editor): guard null model before applyEdits`
- `feat(wasm): add hover provider bridge`
- `ci: align nuget publish workflow`
- `feat!: rename completion provider registration API`
