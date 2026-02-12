# fn-8-fix-ci-test-result-upload-paths-and.1 Fix test result upload paths and filenames in CI workflow

## Description
TBD

## Acceptance
- [ ] TBD

## Done summary
Fixed test result upload paths in all three CI jobs (ubuntu, windows, macos) to use explicit absolute paths under ${{ runner.temp }}/TestResults/ where xUnit v3 actually writes report files, instead of workspace-relative globs that cannot reach runner.temp.
## Evidence
- Commits: fb24cfd53c042bdfaff5ad000e57a5e10d0b3ee5
- Tests: CI workflow change - no local tests applicable
- PRs: