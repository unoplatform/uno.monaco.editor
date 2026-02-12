# fn-10-fix-reverted-emitter-docs-enable-cdp-ci.5 Commit, push, and verify all CI jobs pass

## Description
Commit all changes from tasks 1-4, push to the branch, create or update the PR, and monitor CI until all jobs pass. Fix any CI failures.

**Size:** S
**Files:** All files modified by tasks 1-4

## Approach
- Stage all changes from tasks 1-4
- Create conventional commits: separate commits per logical change (emitter fix, regeneration, CI change, WSL2 profile)
- Push to the epic branch
- Create PR if none exists, or update existing PR
- Run `gh pr checks --watch` to monitor all CI jobs
- If any job fails: investigate logs, fix locally, commit, push, repeat until green
- The epic is NOT done until all CI jobs pass

## Key context
- CI jobs: Build (Ubuntu), Build (macOS ARM), Desktop Tests (Windows), Coverage Report
- Per AGENTS.md CI Verification Policy: "Never leave a PR in a broken CI state"
- Use conventional commit format per AGENTS.md
## Acceptance
- [ ] All changes committed with conventional commit messages
- [ ] PR exists and is up to date with all changes
- [ ] Build (Ubuntu) job passes
- [ ] Build (macOS ARM) job passes
- [ ] Desktop Tests (Windows) job passes (including Desktop CDP tests)
- [ ] Coverage Report job passes
- [ ] `gh pr checks` shows all checks green
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
