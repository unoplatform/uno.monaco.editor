# fn-13-fix-desktop-webview2-runtime-bugs.4 Push to remote and verify CI passes

## Description
Push all fn-13 commits to remote and verify CI passes. The branch has unpushed commits from tasks 1-3 and 6. Per AGENTS.md CI Verification Policy, nothing is done until CI is green.

**Size:** S
**Files:** None (CI verification only)

## Approach
1. `git push` to push all commits to remote
2. If no PR exists, create one targeting `main`
3. `gh pr checks --watch` to monitor CI
4. If any job fails, investigate logs, fix locally, commit, push, and repeat
5. All 4 CI jobs must pass: Build (ubuntu), Build (macOS ARM), Desktop Tests (Windows), Coverage Report

## Acceptance
- [ ] All fn-13 commits pushed to remote
- [ ] PR exists targeting main
- [ ] All CI jobs pass green (Build ubuntu, Build macOS ARM, Desktop Tests Windows, Coverage Report)
- [ ] No regressions in WASM Playwright tests
- [ ] No regressions in Desktop CDP tests

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
