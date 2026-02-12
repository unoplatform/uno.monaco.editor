# fn-4-modernize-ci-add-code-coverage-and.2 Delete stale build directory and update README

## Description
Delete the entire `build/` directory (4 legacy files from Azure DevOps era, none referenced by current CI) and update README.md to remove stale build environment references including UWP framing.

**Size:** S
**Files:** `build/sign-package.ps1` (delete), `build/SignClient.json` (delete), `build/Install-WindowsSdkISO.ps1` (delete), `build/templates/gitversion-run.yml` (delete), `README.md`

## Approach

- `git rm -r build/` — confirmed no references from any current workflow, `.gitignore`, or `.gitattributes`
  - `sign-package.ps1`: legacy SignClient signing (2020), replaced by Azure Key Vault signing in ci.yml
  - `SignClient.json`: config for legacy SignClient tool, references old Azure AD client IDs
  - `Install-WindowsSdkISO.ps1`: UWP Windows SDK installer (2023), project no longer targets UWP
  - `templates/gitversion-run.yml`: Azure DevOps pipeline template using `##vso[task.setvariable]` syntax
- Update `README.md`:
  - **Title/intro** (L1-5): remove or replace "Windows Runtime Component" UWP-only framing — the project now targets browserwasm and desktop (Skia)
  - **Build Notes section** (L76-82): remove Visual Studio 2019 reference, remove Legacy Edge / Monaco v0.22.3 limitation framing, state current requirements: .NET 10 SDK, targets `net10.0-browserwasm` and `net10.0-desktop`
- Verify no dangling references in `.github/**`, `README.md`, or `AGENTS.md` (scoped scan — ignore `.flow/` docs, `packages/build/` in .gitignore, and `dotnet build` commands)

## Key context

- The `.gitignore` entry at L175-176 references `packages/build/` (NuGet packages), NOT the top-level `build/` directory — deletion is safe.
- `.flow/` task docs may reference `build/` historically — these are out of scan scope.
- `SignClient.json` contains Azure AD client IDs in git history — credentials are not revoked by file deletion, but they are not active secrets.
- README.md L3 says "A Windows Runtime Component wrapper" and L82 says it won't move beyond Monaco v0.22.3 due to Legacy Edge — both are outdated.

## Acceptance
- [ ] `build/` directory fully deleted (all 4 files removed)
- [ ] No top-level `build/` references in `.github/**`, `README.md`, or `AGENTS.md` (except `packages/build/` in .gitignore and `dotnet build` commands)
- [ ] README.md title/intro: removed or replaced stale "Windows Runtime Component" UWP-only framing
- [ ] README.md "Build Notes" states: .NET 10 SDK required, targets `net10.0-browserwasm` and `net10.0-desktop`
- [ ] README.md "Build Notes": no VS 2019, no Legacy Edge, no Monaco v0.22.3 limitation

## Done summary
Deleted the entire build/ directory (4 legacy Azure DevOps files: sign-package.ps1, SignClient.json, Install-WindowsSdkISO.ps1, templates/gitversion-run.yml) and modernized README.md to replace UWP "Windows Runtime Component" framing with Uno Platform targeting browserwasm and desktop, updating Build Notes to require .NET 10 SDK with no VS 2019/Legacy Edge/Monaco v0.22.3 references.
## Evidence
- Commits: ac1392f1267be7f8dc4fbbb6860952159a43ff82
- Tests: grep scan for build/ references in .github/**, README.md, AGENTS.md
- PRs: