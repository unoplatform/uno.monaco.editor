# fn-5-comprehensive-documentation-overhaul.1 Write PR reviewer guide for refactoring branch

## Description
Write a comprehensive PR reviewer guide for the current refactoring branch (`ralph-20260211-093916-012f`) being merged into `main`. This branch contains 30+ commits spanning 4 epics of work (fn-1 through fn-4). Reviewers need a roadmap to navigate the massive diff.

**Size:** M
**Files:** `docs/PR-REVIEW-GUIDE.md`

## Approach

- Analyze all commits on the branch via `git log main..HEAD` to understand the full scope
- Group changes by epic and explain the purpose of each:
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

- Branch has commits from all 4 epics — reviewer must understand the sequencing
- Prior art: `MonacoEditorComponent/DesktopContent/bridge-protocol.md` already documents the JSON-RPC wire protocol in detail
- Platform-asymmetric APIs (`AddActionAsync`, `AddCommandAsync`) throw `PlatformNotSupportedException` on desktop — reviewer should verify this is intentional
- `HasGlyphMargin` has a copy-paste XML doc error (says "Get or Set the CodeEditor Text" at `CodeEditor.Properties.cs:137`) — note as known issue
## Acceptance
- [ ] PR reviewer guide exists at `docs/PR-REVIEW-GUIDE.md`
- [ ] Covers all 4 epics (fn-1 Desktop, fn-2 STJ, fn-3 CI, fn-4 Type Gen) with summary of what changed and why
- [ ] Provides recommended reading order for the diff
- [ ] Includes before/after architecture Mermaid diagram
- [ ] Documents breaking changes from 2.0.0-dev.60
- [ ] Lists risk areas and testing coverage
- [ ] Notes package rename (Monaco.Editor → Uno.Monaco.Editor)
- [ ] Includes a reviewer checklist (what to verify per area)
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
