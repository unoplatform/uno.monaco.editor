# fn-7-fix-ci-test-runner-crash-and-add-ci.1 Fix test package versions and verify CI green

## Description
Fix the test runner TypeLoadException that crashes CI on all platforms by updating test infrastructure packages to latest versions.

**Size:** M
**Files:** `Directory.Packages.props`, `MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj`, `tools/MonacoTypeEmitter.Tests/MonacoTypeEmitter.Tests.csproj`

## Approach

1. In `Directory.Packages.props`:
   - Update `xunit.v3.mtp-v2` from 3.2.0 → 3.2.2
   - Update `Microsoft.Testing.Extensions.CodeCoverage` from 18.0.4 → 18.4.1
   - Remove `Microsoft.NET.Test.Sdk` 18.0.1 (redundant with mtp-v2 — this is the root cause of the TypeLoadException)
   - Add `Microsoft.Testing.Extensions.TrxReport` 2.0.2 (structured test result output for CI)

2. In `MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj`:
   - Remove `<PackageReference Include="Microsoft.NET.Test.Sdk" />`
   - Add `<PackageReference Include="Microsoft.Testing.Extensions.TrxReport" PrivateAssets="all" />`

3. In `tools/MonacoTypeEmitter.Tests/MonacoTypeEmitter.Tests.csproj`:
   - Remove `<PackageReference Include="Microsoft.NET.Test.Sdk" />` (present in file, must be removed)
   - Add `<PackageReference Include="Microsoft.Testing.Extensions.TrxReport" PrivateAssets="all" />`
   - Add `<PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" PrivateAssets="all" />` if not already present

4. Verify locally:
   - `dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj` with `--filter-not-trait "Category=DesktopCDP"` (Desktop CDP tests require GUI runner)
   - `dotnet test --project tools/MonacoTypeEmitter.Tests/MonacoTypeEmitter.Tests.csproj`
   - Note: Playwright-based tests may need `pwsh playwright.ps1 install` first; filter them out if not available locally

5. Push to active PR branch, monitor CI via `gh pr checks <PR#> --watch`

6. If CI fails, diagnose from logs, fix, and iterate until all checks pass

## Key context

- `xunit.v3.mtp-v2` bundles Microsoft Testing Platform v2 support — `Microsoft.NET.Test.Sdk` is redundant and causes version conflicts
- The `--coverage` flag in `dotnet test` works with the CodeCoverage extension; no CI workflow changes needed
- TrxReport provides structured XML test results; CI uses `--report-xunit` flags for xUnit format but TrxReport is a recommended companion package for the MTP v2 stack
- CI has 3 test jobs: Build (ubuntu), Build macOS ARM, Desktop Tests (Windows) — all must pass
- Coverage Report job merges `.cobertura.xml` from all platforms

## Acceptance
- [ ] `Directory.Packages.props` updated: xunit.v3.mtp-v2 → 3.2.2, CodeCoverage → 18.4.1, NET.Test.Sdk removed, TrxReport 2.0.2 added
- [ ] `MonacoEditorComponent.Tests.csproj` updated: NET.Test.Sdk ref removed, TrxReport ref added
- [ ] `MonacoTypeEmitter.Tests.csproj` updated: NET.Test.Sdk ref removed, TrxReport ref added
- [ ] Local `dotnet test` passes for MonacoEditorComponent.Tests (filter out DesktopCDP tests)
- [ ] Local `dotnet test` passes for MonacoTypeEmitter.Tests
- [ ] Changes pushed to active PR branch
- [ ] All required CI checks on active PR are green (Build, Build macOS ARM, Desktop Tests, Coverage Report)

## Done summary
Resolved CI TypeLoadException by removing redundant Microsoft.NET.Test.Sdk (conflicted with xunit.v3.mtp-v2's bundled MTP v2), updating xunit.v3.mtp-v2 to 3.2.2 and CodeCoverage to 18.4.1, and adding TrxReport 2.0.2 across both test projects. Also fixed macOS ARM WasmPlaywright timeout and coverage output format.
## Evidence
- Commits: bf53dfd, ac9389b, 8bdd33d
- Tests: dotnet build MonacoEditorComponent.slnx --no-restore, dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj -- --filter-not-trait Category=DesktopCDP, dotnet test --project tools/MonacoTypeEmitter.Tests/MonacoTypeEmitter.Tests.csproj
- PRs: PR #38 CI run 21954352819 all green (Build pass, Build macOS ARM pass, Desktop Tests Windows pass, Coverage Report pass)