# fn-16-multi-file-diff-view-multidiffcodeeditor.2 Rename CodeEditorBase to EditorHostBase

## Description
TBD

## Acceptance
- [ ] TBD

## Done summary
Renamed the four partials and 17 referencing files. Added EditorFlavor {Code,Diff,MultiDiff} because [JSImport] needs a compile-time-constant name, and HasPrimaryDocument to gate the single-document half of BuildInitialStateMap/ApplyInitialPropertyValues.
## Evidence
- Commits:
- Tests: 280/280 unit
- PRs: