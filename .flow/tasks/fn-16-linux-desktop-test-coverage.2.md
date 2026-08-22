# fn-16-linux-desktop-test-coverage.2 Add DesktopSelfVerify fixture and tests

## Description
TBD

## Acceptance
- [ ] TBD

## Done summary
Added DesktopSelfVerifyFixture + 5 tests (Category=DesktopSelfVerify). Found and fixed a pre-existing defect: the app self-verify hover probe could never pass on any platform (provider only reports on the word "Hit"; no loaded text has it at 1:1). All 5 tests pass on Windows in 6.5s.
## Evidence
- Commits:
- Tests:
- PRs: