# fn-6-bug-fixes-disposal-resize-and-test.1 Merge PR #37 WASM resize fix and remove dead layout code

## Description

**Size**: S — cherry-pick + cleanup

**Problem**: The WASM presenter uses `LayoutUpdated` to fire `NativeMethods.RefreshLayout()` on every layout pass, causing excessive JS interop calls and performance issues. PR #37 (https://github.com/unoplatform/uno.monaco.editor/pull/37) replaces this with a TS-side `ResizeObserver` that only fires on actual size changes.

**Files**:
- `MonacoEditorComponent/CodeEditor/WasmCodeEditorPresenter.cs` — remove `LayoutUpdated` handler (~L33-39)
- `MonacoEditorComponent/ts-helpermethods/` — PR #37 adds ResizeObserver in TS
- Any files touched by PR #37 diff

**Approach**:
1. Review PR #37 diff for conflicts with current branch
2. Cherry-pick or merge PR #37 commits
3. Remove the `LayoutUpdated` += handler and `RefreshLayout()` call in `WasmCodeEditorPresenter` if not already done by the PR
4. Validate call-graph for `NativeMethods.RefreshLayout()` — remove the P/Invoke only when no runtime callers remain
5. Verify build succeeds for both browserwasm and desktop targets
6. Smoke-test: confirm editor resizes properly in WASM test app

**Key context**:
- Monaco v0.20+ uses `ResizeObserver` internally when `automaticLayout: true` is set — see https://github.com/microsoft/monaco-editor/issues/3051
- The old `LayoutUpdated` approach fires on every frame, not just resizes
- PR was authored against an older branch; watch for merge conflicts in `WasmCodeEditorPresenter.cs`

## Acceptance
- [ ] PR #37 changes are incorporated into the branch
- [ ] `WasmCodeEditorPresenter` no longer uses `LayoutUpdated` for resize
- [ ] `NativeMethods.RefreshLayout()` removed only after confirming zero runtime callers
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm` succeeds
- [ ] Editor visually resizes when browser window changes in WASM (manual verification note)

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
