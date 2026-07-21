# fn-4-modernize-ci-add-code-coverage-and.3 Add code coverage collection and reporting to CI

## Description
Add full MonacoEditorComponent code coverage collection and merged reporting across all CI test jobs, following the Humanizer project pattern (`Microsoft.Testing.Extensions.CodeCoverage` + ReportGenerator).

Coverage spans three CI jobs:
- **`build` (ubuntu)**: unit tests + WASM Playwright tests (the bulk of coverage)
- **`desktop-tests` (windows)**: DesktopCDP integration tests
- **`build-macos` (macOS)**: WASM tests (overlaps with ubuntu, but catches platform-specific paths)

A final `coverage-report` job merges all results into a single combined report.

**Size:** L
**Files:** `MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj`, `Directory.Packages.props`, `.github/workflows/ci.yml` (all test jobs + new merge job)

**Depends on:** Task .1 (both edit `ci.yml`)

## Approach

Follow the [Humanizer](https://github.com/Humanizr/Humanizer) reference implementation:

1. **Add coverage package via central package management:**
   - Add version entry in `Directory.Packages.props`: `<PackageVersion Include="Microsoft.Testing.Extensions.CodeCoverage" Version="18.0.4" />`
   - Add package reference in `MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj`: `<PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" PrivateAssets="all" />`

2. **Update ALL test-running CI jobs** in `ci.yml`:
   - **`build` job** (ubuntu, L~80): Add `--coverage` and `--results-directory ${{ runner.temp }}/TestResults` to `dotnet test`
   - **`desktop-tests` job** (windows, L~119): Add `--coverage` and `--results-directory ${{ runner.temp }}/TestResults` to `dotnet test`
   - **`build-macos` job** (macOS ARM): Add `--coverage` and `--results-directory ${{ runner.temp }}/TestResults` to `dotnet test`

3. **Upload per-job coverage artifacts:**
   - Each job uploads `${{ runner.temp }}/TestResults/**/*.cobertura.xml` as a uniquely named artifact:
     - `coverage-ubuntu` (from `build` job)
     - `coverage-windows` (from `desktop-tests` job)
     - `coverage-macos` (from `build-macos` job)
   - Use `actions/upload-artifact` with `if-no-files-found: warn` (non-blocking)

4. **Add `coverage-report` merge job:**
   - `needs: [build, desktop-tests, build-macos]`
   - `if: always()` (runs even if some test jobs fail, to get partial coverage)
   - Runs on `ubuntu-latest`
   - Downloads all three coverage artifacts via `actions/download-artifact`
   - Uses `danielpalme/ReportGenerator-GitHub-Action` to merge all Cobertura XMLs:
     - Input: `**/coverage-*/**/*.cobertura.xml`
     - Output: `./CoverageReport/`
     - Report types: `HtmlInline;Cobertura;MarkdownSummaryGithub;Badges`
   - Publishes merged report via `actions/upload-artifact` as `coverage-report`
   - Adds GitHub Step Summary from the MarkdownSummaryGithub output

5. **Add coverage configuration** (optional `testconfig.json` or inline):
   - Format: Cobertura
   - Exclude generated code: `GeneratedCodeAttribute`, `ExcludeFromCodeCoverageAttribute`, `DebuggerHiddenAttribute`, `DebuggerNonUserCodeAttribute`

## Key context

- Humanizer uses `Microsoft.Testing.Extensions.CodeCoverage` v18.0.4 (Microsoft's first-party tool), NOT coverlet.
- This project uses central package management (`ManagePackageVersionsCentrally=true` in `Directory.Build.props`). All package versions must go in `Directory.Packages.props`.
- The `build` job (ubuntu) runs unit tests + WASM Playwright tests via `--filter-not-trait "Category=DesktopCDP"`. This covers the majority of code paths.
- The `desktop-tests` job (windows) runs DesktopCDP integration tests via `--filter-trait "Category=DesktopCDP"`. This covers WebView2/CDP-specific paths.
- The `build-macos` job runs WASM tests (same filter as ubuntu). This overlaps with ubuntu coverage but catches any macOS-specific code paths.
- Humanizer does NOT enforce coverage thresholds — coverage is informational only. Follow this approach.
- ReportGenerator's `MarkdownSummaryGithub` report type generates a GitHub-flavored markdown summary that can be injected into the job summary via `$GITHUB_STEP_SUMMARY`.
- Coverage instrumentation adds ~10-20% overhead to test execution time per job.
- The merge job is lightweight (just downloads artifacts and runs ReportGenerator).

## Acceptance
- [ ] `Microsoft.Testing.Extensions.CodeCoverage` version pinned in `Directory.Packages.props`
- [ ] `MonacoEditorComponent.Tests.csproj` references `Microsoft.Testing.Extensions.CodeCoverage` with `PrivateAssets="all"`
- [ ] `build` CI job (ubuntu) passes `--coverage` and `--results-directory` flags, uploads `coverage-ubuntu` artifact
- [ ] `desktop-tests` CI job (windows) passes `--coverage` and `--results-directory` flags, uploads `coverage-windows` artifact
- [ ] `build-macos` CI job passes `--coverage` and `--results-directory` flags, uploads `coverage-macos` artifact
- [ ] `coverage-report` merge job downloads all coverage artifacts, runs ReportGenerator, uploads combined `coverage-report`
- [ ] GitHub Step Summary shows coverage summary from merged report
- [ ] Generated/excluded code filtered from coverage
- [ ] Coverage collection does not break existing test execution in any job
- [ ] Merge job runs with `if: always()` so partial coverage is available when some jobs fail

## Done summary
Added code coverage collection and merged reporting to CI. Microsoft.Testing.Extensions.CodeCoverage v18.0.4 is added to the test project, all three CI test jobs (ubuntu, windows, macOS) now pass --coverage and --results-directory flags with per-job artifact uploads, and a new coverage-report merge job uses ReportGenerator to produce combined HTML/Cobertura reports with GitHub Step Summary output.
## Evidence
- Commits: 5371fa48da7505956a50db26a62a905b06fdb530
- Tests: dotnet restore MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj, dotnet build MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj --no-restore
- PRs: