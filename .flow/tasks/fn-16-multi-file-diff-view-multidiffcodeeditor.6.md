# fn-16-multi-file-diff-view-multidiffcodeeditor.6 Sample app and integration tests

## Description
TBD

## Acceptance
- [ ] TBD

## Done summary
Sample covering all four file states, plus 10 desktop CDP and 2 WASM Playwright tests. Caught three real defects: an object[] overload trap that broke every WASM push, content-space scroll arithmetic in reveal, and a subscription-bookkeeping bug that detached reused entries.
## Evidence
- Commits:
- Tests:
- PRs: