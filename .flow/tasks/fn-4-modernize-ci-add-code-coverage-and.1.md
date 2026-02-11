# fn-4-modernize-ci-add-code-coverage-and.1 Update CI runner images and GitHub Action versions

## Description
Update CI runner images and GitHub Action versions to latest in the targeted composite actions and main CI workflow.

**Policy:** Standardize on `@v4` for `actions/checkout` and `actions/setup-dotnet`. The `copilot-setup-steps.yml` (@v5) and `nuget-org-publish/action.yml` (no checkout/setup-dotnet) are out of scope.

**Size:** M
**Files:** `.github/workflows/ci.yml`, `.github/actions/tag-release/action.yml`, `.github/actions/nuget-uno-publish/action.yml`

## Approach

- In `ci.yml` L161: change `runs-on: macos-15` to `runs-on: macos-26` for the `build-macos` job
- In `ci.yml` L249: change signing description from `"Uno Monaco Editor UWP"` to `"Uno Monaco Editor"` (UWP label is stale)
- In `tag-release/action.yml`:
  - L8: `actions/checkout@v2` → `actions/checkout@v4`
  - L11: `actions/setup-dotnet@v1` → `actions/setup-dotnet@v4`
  - L13: `dotnet-version: '9.0.201'` → `dotnet-version: '10.0.x'`
  - L19: `toolVersion: 3.6.139` → `toolVersion: 3.8.118` (aligns with ci.yml L50)
- In `nuget-uno-publish/action.yml`:
  - L20-21: `dotnet-version: '9.0.x'` → `dotnet-version: '10.0.x'`

## Key context

- macOS 26 is public beta (since Sep 2025). .NET 10.0.102 is preinstalled. Default Xcode is 26.2.
- **Rollback rule:** if `build-macos` fails due to runner-image issues for 3+ consecutive runs, revert to `macos-15`. The job gates `sign` via `needs: [build, desktop-tests, build-macos]`.
- The `tag-release` action is used in `publish_release_nuget_org` (ci.yml L314) — production release path. NBGV 3.8.118 is already in the main build (ci.yml L50), so aligning reduces skew.
- The `tag-release` action has its own `actions/checkout` step (L7-8) even though the caller job already checks out. Keep it but update to v4 — removing it would change composite action behavior.

## Acceptance
- [ ] `build-macos` job uses `runs-on: macos-26`
- [ ] `tag-release/action.yml` uses `actions/checkout@v4` and `actions/setup-dotnet@v4`
- [ ] `tag-release/action.yml` uses `dotnet-version: '10.0.x'` and NBGV `toolVersion: 3.8.118`
- [ ] `nuget-uno-publish/action.yml` uses `dotnet-version: '10.0.x'`
- [ ] Signing description says `"Uno Monaco Editor"` (no "UWP")
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` passes (no local breakage)

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
