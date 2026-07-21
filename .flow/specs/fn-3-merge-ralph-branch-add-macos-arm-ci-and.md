# Merge ralph branch, add macOS ARM CI, and get CI green

## Overview

Merge the ralph branch (commits spanning fn-1 Desktop Skia target + fn-2 STJ migration) into `dev/cnov/desktop-head`, push upstream, add multi-platform CI testing (macOS ARM + Windows + Linux), and ensure CI passes on PR #38.

## Scope

- Fix the blocking Uno.Resizetizer `GenerateWasmSplashAssets` CI error
- Add macOS ARM (`macos-15`) CI job for cross-platform build+test verification
- Clean up CI workflow (narrow desktop-tests build scope, add concurrency groups, gate sign on all quality jobs)
- Add `.gitattributes` linguist-generated markers for reviewable PR diffs
- Fast-forward merge ralph → desktop-head → push to origin
- Update PR #38 metadata to reflect full scope (fn-1 + fn-2)
- Monitor CI until all jobs pass

## Key Context

### Branch Topology (all fast-forward, no conflicts)
```
main (fc42320)
  └── 9 commits → dev/cnov/desktop-head (ce90741) [remote exists]
                     └── many commits → ralph-20260211-093916-012f [HEAD, local only]
```

### Current CI Architecture (`.github/workflows/ci.yml`)
- `build` job (ubuntu-latest): solution build + WASM test app + non-DesktopCDP tests
- `desktop-tests` job (windows-latest): **solution-wide build** + DesktopCDP Playwright tests
- `sign/publish` jobs: gated on push to main/release, currently `needs: build` only

### CI Blocker
Uno.Resizetizer 1.12.1 `GenerateWasmSplashAssets` task fails on ubuntu in the `Build WASM test app` step. This is a transitive dependency from Uno.Sdk 6.5.31, not a code defect.

### Platform Test Constraints
- **DesktopCDP tests**: Windows-only (WebView2 CDP). macOS uses WKWebView, Linux uses WebKitGTK — neither supports CDP.
- **macOS ARM CI**: Can run unit tests, serialization tests, WASM Playwright tests. Skip DesktopCDP.
- **Uno Skia Desktop on macOS**: Auto-falls back to software rendering when Metal unavailable (GitHub CI VMs).

### Reference Patterns
- `unoplatform/uno.templates` CI: three parallel OS jobs (macos-15, ubuntu-latest, windows-latest) + test-validation gate
- `macos-15` runners are ARM64 (Apple Silicon), .NET 10.0.102 pre-installed

### Build Scope Constraints
- **desktop-tests job** builds the solution which includes `net10.0-browserwasm` targets. If wasm workloads are removed, the build must be narrowed to desktop-only projects/TFMs to avoid WASM build failures.
- Either keep wasm workloads in desktop-tests, or change the build command to target only desktop TFMs.

## Task Dependency DAG
```
.2 (Resizetizer fix + .gitattributes) ─┐
                                         ├── .1 (merge + push) → .4 (PR metadata + monitor)
.3 (macOS CI job + CI cleanup)         ─┘
```
Tasks .2 and .3 touch disjoint files and run in parallel. Task .1 merges all changes into desktop-head and pushes. Task .4 updates PR metadata and monitors CI.

## Quick commands
```bash
# Verify branch topology
git merge-base --is-ancestor dev/cnov/desktop-head ralph-20260211-093916-012f && echo "fast-forward safe"

# Build locally
dotnet build MonacoEditorComponent.slnx -c Release

# Run non-desktop tests
dotnet test MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj -- --filter-not-trait "Category=DesktopCDP"

# Monitor CI after push
gh pr checks 38 --repo unoplatform/uno.monaco.editor --watch
```

## Acceptance

- [ ] Resizetizer CI blocker resolved (WASM test app builds on ubuntu CI — verified via CI log, not just local build)
- [ ] macOS ARM CI job added (`macos-15` runner) running non-DesktopCDP tests
- [ ] Desktop tests pass on Windows (`desktop-tests` job)
- [ ] `desktop-tests` job wasm workloads removed AND build narrowed to desktop-only project/TFM
- [ ] `sign` job depends on all quality gates (`build`, `desktop-tests`, `build-macos`) — mandatory, not optional
- [ ] Test artifact names are job-qualified to avoid cross-job collisions (e.g., `test-artifacts-ubuntu`, `test-artifacts-macos`)
- [ ] WASM + unit + serialization tests pass on Ubuntu (`build` job)
- [ ] WASM + unit + serialization tests pass on macOS ARM (new job)
- [ ] ralph branch merged into dev/cnov/desktop-head (fast-forward, verified via ancestry check)
- [ ] dev/cnov/desktop-head pushed to origin
- [ ] PR #38 title/body updated to reflect fn-1 + fn-2 scope
- [ ] `.gitattributes` has linguist-generated markers for generated C# typings (`Monaco/Editor/**`, `Monaco/Helpers/**`, `Monaco/Languages/**`) and machine-generated JSON (`.flow/**/*.json`) — NOT hand-authored markdown specs or helper .cs files
- [ ] All CI checks green on PR #38

## Known Gaps
- Linux desktop validation: build-only (no CDP integration tests possible on Linux)
- macOS desktop CDP tests: not possible (WKWebView has no CDP support)

## Risks
- Resizetizer fix may require Uno.Sdk version bump (could introduce other changes)
- macOS ARM Playwright browser install may behave differently than Ubuntu
- Large PR diff (427 files) may cause GitHub UI performance issues (mitigated by linguist-generated)
- Removing wasm workloads from desktop-tests without narrowing build scope will break the job
