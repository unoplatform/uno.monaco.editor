# fn-3-merge-ralph-branch-add-macos-arm-ci-and.3 Add macOS ARM CI job and clean up CI workflow

## Description
Add a new `build-macos` job to `.github/workflows/ci.yml` that runs on `macos-15` (ARM64) for cross-platform verification. Also clean up the existing `desktop-tests` job by narrowing its build scope and removing unnecessary workloads, add concurrency groups for PR efficiency, and make `sign` depend on all quality gates.

**Size:** M
**Files:** `.github/workflows/ci.yml`

## Approach

### New macOS ARM job

Add `build-macos` job following the `unoplatform/uno.templates` pattern (three parallel OS jobs). The macOS job should:

1. Run on `macos-15` (ARM64 Apple Silicon)
2. Checkout with `fetch-depth: 0` (needed for NBGV)
3. Setup .NET 10.0.x via `actions/setup-dotnet@v4`
4. Install `wasm-tools` workload (needed for WASM test app build)
5. Build solution (`dotnet build MonacoEditorComponent.slnx -c Release`)
6. Build WASM test app (`-f net10.0-browserwasm`)
7. Build desktop test app (`-f net10.0-desktop`) — verifies macOS Skia compilation
8. Install Playwright Chromium (same pattern as existing build job, ci.yml:75-81)
9. Run tests with `--filter-not-trait "Category=DesktopCDP"` (same filter as ubuntu build job)
10. Upload test artifacts on failure with job-qualified name (`test-artifacts-macos`)

The job should run in parallel with `build` (no `needs` dependency) for faster CI.

### CI cleanup: desktop-tests build scope

**Critical**: The `desktop-tests` job currently does a solution-wide build (`dotnet build MonacoEditorComponent.slnx`). The solution includes `MonacoEditorTestApp` which targets `net10.0-browserwasm;net10.0-desktop`. Removing wasm workloads without narrowing build scope will break the job.

**Concrete approach**: Remove `wasm-tools` workloads AND narrow the build to desktop-only:
1. Remove `wasm-tools wasm-tools-net9` from the workload install step
2. Replace `dotnet build MonacoEditorComponent.slnx -c Release` with `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop -c Release`
3. Also build the test project: `dotnet build MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj -c Release`

This keeps the job focused on its purpose (desktop-only testing) without wasting time on WASM builds.

### Concurrency groups

Add top-level workflow-scoped concurrency group to cancel superseded PR runs without colliding with other workflows:
```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```

### Sign job gating (mandatory)

Change `sign` job to `needs: [build, desktop-tests, build-macos]` so that signing/publishing is blocked if any quality gate fails. This is mandatory, not optional.

### Artifact name deconfliction

Use job-qualified artifact names to prevent cross-job collisions:
- `build` job: `test-artifacts-ubuntu`
- `build-macos` job: `test-artifacts-macos`
- `desktop-tests` job: `test-artifacts-windows` (if it uploads artifacts)

## Key context

- `macos-15` runners: ARM64, 3 cores, 7GB RAM, .NET 10.0.102 pre-installed
- Uno Skia Desktop on macOS auto-falls back to software rendering in CI (no Metal in VMs) — this is expected
- Desktop CDP tests CANNOT run on macOS (WKWebView has no CDP) — must filter them out
- Playwright supports ARM64 Chromium on macOS natively (since Playwright 1.40+)
- Memory pitfall: "Playwright NuGet build/buildTransitive targets conflict with UseArtifactsOutput+OutputType=Exe on macOS/Linux"
- `wasm-tools-net9` workload: verify still needed with .NET 10 SDK, remove if not

## Acceptance
- [ ] New `build-macos` job in ci.yml runs on `macos-15`
- [ ] macOS job builds solution, WASM test app, and desktop test app
- [ ] macOS job runs non-DesktopCDP tests with Playwright Chromium
- [ ] macOS job runs in parallel with existing `build` job (no serial dependency)
- [ ] `desktop-tests` wasm workloads removed AND build narrowed to desktop-only project/TFM
- [ ] `sign` job `needs: [build, desktop-tests, build-macos]` (all quality gates)
- [ ] Concurrency group is workflow-scoped (uses `github.workflow` prefix) to avoid cross-workflow collisions
- [ ] Artifact names are job-qualified (e.g., `test-artifacts-ubuntu`, `test-artifacts-macos`)
- [ ] Changes committed to current branch

## Done summary
Added macOS ARM CI job (`build-macos`) on `macos-15`, cleaned up `desktop-tests` by removing wasm workloads and narrowing build scope, added workflow-scoped concurrency group, gated `sign` on all quality gates, and renamed artifact names to be job-qualified.
## Evidence
- Commits: eadd0d3
- Tests:
- PRs: