# fn-16-linux-desktop-test-coverage.1 Extract reusable process/log harness from DesktopAppFixture

## Description
TBD

## Acceptance
- [ ] TBD

## Done summary
Extracted TestAppProcessHost (launch + log capture + cursor query API) from DesktopAppFixture; fixture forwards its public log API unchanged. Capture window is now a parameter. Tests build clean; 42 CDP tests unaffected (Windows run pending in CI).
## Evidence
- Commits:
- Tests:
- PRs: