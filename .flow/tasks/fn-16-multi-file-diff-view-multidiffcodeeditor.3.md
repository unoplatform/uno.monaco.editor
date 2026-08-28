# fn-16-multi-file-diff-view-multidiffcodeeditor.3 Adopt the house dependency property style

## Description
TBD

## Acceptance
- [ ] TBD

## Done summary
All 16 existing DPs moved onto the DrawerControl/codegen style: file-scoped namespace, header comment block, DefaultValues holder, one region per property, named static callbacks forwarding to instance methods in the sibling partial. IsEditorLoaded and RenderingBackend relocated into the .Properties.cs file.
## Evidence
- Commits:
- Tests: 280/280 unit, no test edits required
- PRs: