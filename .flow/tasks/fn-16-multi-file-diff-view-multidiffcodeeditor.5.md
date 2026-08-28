# fn-16-multi-file-diff-view-multidiffcodeeditor.5 MultiDiffCodeEditor control and host plumbing

## Description
TBD

## Acceptance
- [ ] TBD

## Done summary
MultiDiffCodeEditor + DiffFileEntry + Generic.xaml template + the third [JSImport]. Files is an observable collection of observable entries; every change re-pushes the whole list because the JS side reconciles incrementally.
## Evidence
- Commits:
- Tests:
- PRs: