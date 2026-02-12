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
4. If the branch has an active PR, verify CI passes after pushing (see CI Verification Policy below).

## CI Verification Policy

Branches with active pull requests must have CI verified green before marking tasks or epics as done. Never leave a PR in a broken CI state -- fix failures before moving on.

### When it applies

This policy applies whenever your working branch has an open pull request. At minimum, verify CI before marking an epic done. Ideally, check CI after every significant push.

### CI job structure

The CI pipeline (`.github/workflows/ci.yml`) runs the following jobs on pull requests:

| Job | Runner | What it validates |
|-----|--------|-------------------|
| **Build** | `ubuntu-latest` | Library build, test build, WASM app build, Playwright browser tests, code coverage |
| **Build (macOS ARM)** | `macos-26` | Library build, test build, WASM + desktop app builds, unit tests, code coverage |
| **Desktop Tests (Windows)** | `windows-latest` | Desktop build compilation, unit tests (depends on Build) |
| **Coverage Report** | `ubuntu-latest` | Merges coverage from all platforms (depends on all test jobs) |

Additional jobs (Sign, Publish Dev, Publish Production) run only on pushes to `main` or `release/*` branches and are not triggered by PRs.

### Known CI limitations

- **Desktop CDP tests** are excluded from all CI runners (`--filter-not-trait "Category=DesktopCDP"`). WebView2 CDP tests require a GUI environment; GitHub Actions runners are headless, so these tests timeout on fixture initialization. They must be validated locally.
- **WASM Playwright tests** are excluded from the Windows Desktop Tests job and the macOS ARM job (`--filter-not-trait "Category=WasmPlaywright"`). Ubuntu covers WASM Playwright tests; macOS ARM validates builds and unit tests only (the static file server startup is too slow on ARM runners).

### How to verify

Use the GitHub CLI to monitor CI status after pushing:

```bash
# Watch all checks on the current PR until they complete
gh pr checks --watch

# Check a specific PR by number
gh pr checks 38 --watch
```

You can also view the GitHub Actions UI directly from the PR page.

### What to do when CI fails

1. Push your changes to the remote branch.
2. Run `gh pr checks --watch` to monitor the pipeline.
3. If any job fails, investigate the failure logs (`gh pr checks` shows URLs to failed runs).
4. Fix the issue locally, commit, push, and repeat until all jobs pass.
5. Only after CI is green, proceed with marking the task or epic as done.

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
