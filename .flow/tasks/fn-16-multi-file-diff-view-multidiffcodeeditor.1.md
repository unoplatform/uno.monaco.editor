# fn-16-multi-file-diff-view-multidiffcodeeditor.1 Spike: prove Monaco's multi-diff widget is drivable

## Description
TBD

## Acceptance
- [ ] TBD

## Done summary
Proved in Chromium that MultiDiffEditorWidgetImpl can be constructed directly and driven: filename headers, chevrons, unified scroll, per-file hunks, read-only, A/D/R badges, resize. createMultiFileDiffEditor itself is unusable -- tree-shaken to a constructor, hardcoded empty UI factory.
## Evidence
- Commits:
- Tests:
- PRs: