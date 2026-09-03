# fn-16-multi-file-diff-view-multidiffcodeeditor.4 TypeScript multi-file diff layer

## Description
TBD

## Acceptance
- [ ] TBD

## Done summary
multiDiffEditor.ts constructs the widget through four deep ESM imports, with a symbol guard and post-construction DOM/theme assertions. Owns every ITextModel, reconciles by path, defers disposal a frame, pushes an explicit Dimension on resize.
## Evidence
- Commits:
- Tests:
- PRs: