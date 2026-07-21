# fn-3-merge-ralph-branch-add-macos-arm-ci-and.2 Fix Resizetizer CI blocker and add .gitattributes markers

## Description
Fix the Uno.Resizetizer `GenerateWasmSplashAssets` CI blocker that prevents the WASM test app from building on ubuntu-latest. Also add `linguist-generated` markers to `.gitattributes` so GitHub collapses generated file diffs in the PR.

**Size:** M
**Files:** `MonacoEditorTestApp/MonacoEditorTestApp.csproj`, `.gitattributes`

## Approach

### Resizetizer fix

The error is `MSB4181: The "GenerateWasmSplashAssets_..." task returned false` from Uno.Resizetizer 1.12.1 (transitive via Uno.Sdk 6.5.31). Options:

1. **Preferred**: Add `<UnoDisableSplashScreen>true</UnoDisableSplashScreen>` to MonacoEditorTestApp.csproj — this is a test app, splash screen is irrelevant
2. **Alternative**: Bump Uno.Sdk version if a fix exists upstream
3. Verify fix by building: `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm -c Release`

### .gitattributes markers

Add `linguist-generated=true` for generated code subdirectories and machine-generated metadata only:
- `MonacoEditorComponent/Monaco/Editor/**/*.cs` (generated from typings)
- `MonacoEditorComponent/Monaco/Helpers/**/*.cs` (generated from typings)
- `MonacoEditorComponent/Monaco/Languages/**/*.cs` (generated from typings)
- `.flow/**/*.json` (machine-generated metadata: epics, tasks, checkpoints, reviews, config)
- `.flow/usage.md` (plugin-generated usage guide)

**Excluded** (hand-authored, must remain visible in PR diff):
- `MonacoEditorComponent/Monaco/LanguagesHelper.cs` (hand-authored helper)
- `MonacoEditorComponent/Monaco/LanguagesHelper.Additions.cs` (hand-authored helper)
- `MonacoEditorComponent/Monaco/ModelHelper.cs` (hand-authored helper)
- `.flow/specs/*.md` (hand-authored epic specs)
- `.flow/tasks/*.md` (hand-authored task specs)
- `.flow/memory/*.md` (hand-authored project memory)

**Removed from original plan** (stale path — does NOT exist in source tree):
- ~~`MonacoEditorComponent/monaco-editor/**`~~ — vendored Monaco is only in build artifacts, not tracked in git

**Verify**: Each glob path must exist in the source tree before adding a marker. Run `ls` to confirm.

This collapses the majority of generated/machine-generated file diffs while keeping all hand-authored code and specs visible for review.

## Key context

- The Resizetizer error blocks the `Build WASM test app` step (ci.yml:73) before any tests run
- Uno.Resizetizer is a transitive dependency — no explicit PackageReference to pin
- `.gitattributes` currently has no linguist-generated entries
- PR #38 has 427 files changed / ~100k lines — GitHub UI is unusable without collapsing generated files
- Monaco/Editor/, Monaco/Helpers/, Monaco/Languages/ are all generated; root-level files include 3 hand-authored helpers

## Acceptance
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm -c Release` succeeds locally
- [ ] CI `build` job on ubuntu passes WASM test app build (verified after push in task .4)
- [ ] `.gitattributes` has linguist-generated markers for `Monaco/Editor/**`, `Monaco/Helpers/**`, `Monaco/Languages/**`, `.flow/**/*.json`, `.flow/usage.md`
- [ ] `.gitattributes` does NOT mark hand-authored helper files or `.flow/*.md` specs as generated
- [ ] Each glob path verified to exist in source tree before adding marker
- [ ] Changes committed to current branch

## Done summary
Disabled Uno.Resizetizer splash screen generation in test app to fix GenerateWasmSplashAssets CI blocker on ubuntu-latest, and added linguist-generated markers to .gitattributes for generated Monaco typings and machine-generated .flow metadata while excluding hand-authored helpers and specs.
## Evidence
- Commits: 94ea3d8e2af16724f7006d5d9f71bc2e2e5c7064
- Tests: dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm -c Release, dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj --no-build -- --filter-not-trait Category=DesktopCDP --filter-not-trait Category=WasmPlaywright
- PRs: