# fn-13-fix-desktop-webview2-runtime-bugs.3 Fix null selection NullRef and test app defensive error handling

## Description
Fix two remaining issues:

1. **NullRef on SetSelectedText with no selection (Bug 4):** Pressing "Set Selected Text" in the test app when no text is selected causes a NullReferenceException. The TS function `updateSelectedContent` at `updateSelectedContent.ts:7` uses `editorContext.editor.getSelection()!` — the non-null assertion can fail. Additionally, the C# side `ButtonSetSelectedText_Click` at `EditorControl.xaml.cs:655-658` unconditionally sets `SelectedText` without checking editor state.

2. **Test app defensive error handling:** `Editor_Loading` at `EditorControl.xaml.cs:102` is `async void` with no top-level try-catch. If `StorageFile.GetFileFromApplicationUriAsync` fails (file missing, permissions), the exception propagates to SynchronizationContext and can crash the app.

**Size:** S
**Files:**
- `MonacoEditorComponent/ts-helpermethods/updateSelectedContent.ts` (null/collapsed selection guard)
- `MonacoEditorTestApp/EditorControl.xaml.cs` (ButtonSetSelectedText_Click guard, Editor_Loading try-catch)

## Approach

- **Primary fix (TypeScript):** Add guard in `updateSelectedContent.ts` to check if `getSelection()` returns null or a collapsed range (start === end). If no meaningful selection exists, return early without calling `pushEditOperations` (no-op). This matches the "Set Selected Text" button label: if nothing is selected, nothing happens.
- **Secondary fix (test app safety):** Wrap `Editor_Loading` body in try-catch. Add `IsEditorLoaded` check in `ButtonSetSelectedText_Click` before setting `SelectedText` to demonstrate correct API usage patterns.
- Follow pattern from BlazorMonaco (found by github-scout): `GetSelection()` returns `Task<Selection>` — nullable-aware, no assertion.

## Key context

- `getSelection()` on Monaco `IStandaloneCodeEditor` returns `Selection | null`. When the editor has focus, it returns a `Selection` which may be collapsed (start == end). When the editor has no model, it can return null.
- The test app button handler is sample code — but it should demonstrate correct usage patterns for consumers.
- `updateSelectedContent` is called from `CodeEditor.Properties.cs:78` via `InvokeScriptAsync`. The `SelectedText` DP change handler already checks `IsEditorLoaded`.
- The primary fix is in TypeScript (prevents the NRE at its source). The C# guard is defensive and demonstrates correct API usage.

## Acceptance
- [ ] "Set Selected Text" button with no selection does NOT throw NullReferenceException
- [ ] When no text is selected (`getSelection()` returns null or collapsed range), `updateSelectedContent` returns early (no-op) without calling `pushEditOperations`
- [ ] `Editor_Loading` in test app has try-catch wrapping async operations
- [ ] `ButtonSetSelectedText_Click` checks `IsEditorLoaded` before setting `SelectedText`
- [ ] Existing selection-based tests (if any) continue to pass
- [ ] TS helpers build clean (`npm run build`)
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
