# fn-10-fix-reverted-emitter-docs-enable-cdp-ci.7 Fix Playwright package installation per official docs

## Description
The test project incorrectly uses raw `Microsoft.Playwright` with heavy asset exclusion hacks instead of the official `Microsoft.Playwright.Xunit.v3` package designed for xUnit v3. The current comment claims the xUnit.v3 adapter "causes fixture disposal hangs on CI" but this was a misdiagnosis — the disposal hangs were caused by incorrect fixture usage patterns, not the adapter itself.

Additionally, the CI workflow uses a brittle manual hack to install Playwright browsers: it loads `Microsoft.Playwright.dll` from the NuGet global cache via reflection and calls `Program.Main`. With the proper package, the Playwright build targets produce a `playwright.ps1` script in the build output directory. The CI should use that script per the official docs.

**Current package (wrong):**
```xml
<PackageReference Include="Microsoft.Playwright" IncludeAssets="compile;runtime" PrivateAssets="contentFiles;build;buildTransitive;analyzers;native" />
```

**Should be (per https://playwright.dev/dotnet/docs/intro):**
```xml
<PackageReference Include="Microsoft.Playwright.Xunit.v3" />
```

**Current CI install (wrong — manual DLL reflection hack):**
```pwsh
$globalPkgs = dotnet nuget locals global-packages --list | ...
$pwDll = Join-Path $pwPkg.FullName "lib/netstandard2.0/Microsoft.Playwright.dll"
[Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($pwDll)) | Out-Null
[Microsoft.Playwright.Program]::Main(@("install", "--with-deps", "chromium"))
```

**Should be (per official docs — use playwright.ps1 from build output):**
```pwsh
pwsh <build-output-dir>/playwright.ps1 install --with-deps chromium
```

With `UseArtifactsOutput=true`, build output goes to `artifacts/bin/MonacoEditorComponent.Tests/<config>/`. The `playwright.ps1` script will be there once the Playwright build targets are no longer excluded. Each CI job should reference its own build output path.

**Size:** S
**Files:** `MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj`, `Directory.Packages.props`, `.github/workflows/ci.yml`

## Approach
- Replace `Microsoft.Playwright` with `Microsoft.Playwright.Xunit.v3` in `Directory.Packages.props`
- Remove the `IncludeAssets` and `PrivateAssets` attributes from the csproj PackageReference
- Remove the misleading comment about reverting from Xunit.v3
- **Standardize artifacts path across all 3 CI jobs:** Currently Ubuntu passes explicit `-p:ArtifactsPath="${{ env.ARTIFACTS_DIR }}"` but Windows/macOS don't. Add `-p:ArtifactsPath` to Windows and macOS build steps too, using a job-level env var so all paths are derived from one source.
- Update all 3 "Install Playwright browsers" steps in `.github/workflows/ci.yml`:
  - All 3 jobs: derive `playwright.ps1` path from the same artifacts env var: `$env:ARTIFACTS_PATH/bin/MonacoEditorComponent.Tests/release/playwright.ps1`
  - Ubuntu job: `ARTIFACTS_PATH=${{ env.ARTIFACTS_DIR }}`
  - Windows job: `ARTIFACTS_PATH=artifacts` (or explicit `-p:ArtifactsPath`)
  - macOS job: `ARTIFACTS_PATH=artifacts` (or explicit `-p:ArtifactsPath`)
- Each CI step must include a `Test-Path` guard before invoking `playwright.ps1`:
  ```pwsh
  $scriptPath = "$env:ARTIFACTS_PATH/bin/MonacoEditorComponent.Tests/release/playwright.ps1"
  if (-not (Test-Path $scriptPath)) { throw "playwright.ps1 not found at $scriptPath — verify build output path" }
  pwsh $scriptPath install --with-deps chromium
  ```
- Remove `PLAYWRIGHT_DRIVER_SEARCH_PATH` env var setup (no longer needed when build targets run properly)
- Verify `dotnet build` succeeds for the test project
- The existing `WasmAppFixture` and `DesktopAppFixture` will continue to work since they manually create `IPlaywright` (the base classes are optional, not mandatory)

## Key context
- Official Playwright .NET docs: https://playwright.dev/dotnet/docs/intro
- The `Microsoft.Playwright.Xunit.v3` package provides `PageTest`, `BrowserTest`, `ContextTest`, `PlaywrightTest` base classes
- The base classes are for standard browser testing; CDP testing (our DesktopAppFixture) will still manually create IPlaywright — that's fine and expected
- The WasmAppFixture could optionally inherit from a Playwright base class but doesn't need to for now
- The `ExcludeAssets` hack was originally added to work around a build conflict with `UseArtifactsOutput=true` but the proper fix is using the adapter package which handles this correctly
- CI install steps exist in 3 places: ubuntu `build` job (line ~90), windows `desktop-tests` job (line ~168), macOS `build-macos` job (line ~236)

## Acceptance
- [ ] `Microsoft.Playwright.Xunit.v3` replaces `Microsoft.Playwright` in both csproj and Directory.Packages.props
- [ ] No `IncludeAssets` or `PrivateAssets` attributes on the Playwright PackageReference
- [ ] Misleading "reverted from Xunit.v3" comment removed
- [ ] All 3 CI "Install Playwright browsers" steps use `playwright.ps1` from the build output directory (no manual DLL loading)
- [ ] Each CI step has explicit `Test-Path` guard before invoking `playwright.ps1`
- [ ] All 3 CI jobs use explicit `-p:ArtifactsPath` for build steps (no implicit defaults)
- [ ] Playwright script path derived from shared env var (no hardcoded path drift)
- [ ] No `PLAYWRIGHT_DRIVER_SEARCH_PATH` env var hacks in CI
- [ ] `dotnet build MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj` succeeds
- [ ] Existing test fixtures (`WasmAppFixture`, `DesktopAppFixture`) still compile and work

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
