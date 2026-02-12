## Description
Write a comprehensive PR reviewer guide for the current refactoring branch (`ralph-20260211-093916-012f`) being merged into `main`. This branch contains 30+ commits spanning 4 epics of work (fn-1 through fn-4). Reviewers need a roadmap to navigate the massive diff.

**Size:** M
**Files:** `docs/PR-REVIEW-GUIDE.md`

## Approach

- **Pin exact commit range**: Record base SHA (`git merge-base main HEAD`), head SHA, and commit count at time of writing. Include these in the document header.
- Analyze all commits via `git log main..HEAD` to understand the full scope
- Group changes by epic:
  - **fn-1**: Desktop Skia target — `ICodeEditorPresenter` abstraction, `DesktopCodeEditorPresenter`, JSON-RPC bridge, WebView2 integration
  - **fn-2**: STJ migration — Newtonsoft.Json removal, `MonacoJsonContext` source generator, `BridgeSerializerContext`, custom enum converters
  - **fn-3**: CI modernization — GitHub Actions, Playwright tests, code coverage
  - **fn-4**: Type generation pipeline — ts-morph extractor, .NET CLI emitter, Monaco type regeneration
- Provide a recommended reading order (commit-by-commit or file-group-by-file-group)
- Include risk assessment: what areas could break, what was tested, edge cases
- Add a Mermaid diagram showing before/after architecture (single-platform → dual-platform)
- Follow dotnet/runtime 3-step review pattern with severity labels (error/warning/suggestion)
- Document the package rename (`Monaco.Editor` → `Uno.Monaco.Editor`)

## Key context

- Prior art: `MonacoEditorComponent/DesktopContent/bridge-protocol.md` documents JSON-RPC wire protocol
- Platform-asymmetric APIs (`AddActionAsync`, `AddCommandAsync`) throw `PlatformNotSupportedException` on desktop
- `HasGlyphMargin` has a copy-paste XML doc error at `CodeEditor.Properties.cs:137` — note as known issue

## Acceptance
- [x] PR reviewer guide exists at `docs/PR-REVIEW-GUIDE.md`
- [x] Document header includes pinned commit range: base SHA, head SHA, commit count
- [x] Covers all 4 epics with summary of what changed and why
- [x] Provides recommended reading order for the diff
- [x] Includes before/after architecture Mermaid diagram
- [x] Documents breaking changes from 2.0.0-dev.60
- [x] Lists risk areas and testing coverage
- [x] Notes package rename (Monaco.Editor → Uno.Monaco.Editor)
- [x] Includes a reviewer checklist (what to verify per area)

## Done summary

Wrote comprehensive PR reviewer guide at `docs/PR-REVIEW-GUIDE.md` covering all 4 epics (fn-1 Desktop Skia, fn-2 STJ migration, fn-3 CI stabilization, fn-4 CI modernization + type generation), with pinned commit range, before/after Mermaid architecture diagrams, lifecycle state machine, breaking changes table with migration guidance, risk assessment, testing coverage summary, and per-epic reviewer checklist.

## Evidence

- Commits: `49614d4`, `986c291`, `ea4d754`
- Review: RepoPrompt impl-review, 2 fix rounds, SHIP verdict
- Output: `docs/PR-REVIEW-GUIDE.md` (390+ lines)
