# fn-3-merge-ralph-branch-add-macos-arm-ci-and.1 Merge ralph into desktop-head and push upstream

## Description
Fast-forward merge the ralph branch into `dev/cnov/desktop-head` and push the updated branch to origin. This updates PR #38 with all commits on `ralph-20260211-093916-012f` at merge time from the two completed epics plus CI fixes.

**Size:** S
**Files:** none (git operations only)

## Approach

1. Verify fast-forward is safe: `git merge-base --is-ancestor dev/cnov/desktop-head ralph-20260211-093916-012f`
2. Fast-forward desktop-head: `git checkout dev/cnov/desktop-head && git merge --ff-only ralph-20260211-093916-012f`
3. Push to origin: `git push origin dev/cnov/desktop-head`
4. Verify PR #38 updated: `gh pr view 38 --repo unoplatform/uno.monaco.editor`

## Key context

- Ralph is a strict superset of desktop-head (many commits ahead, 0 behind). Merge-base = desktop-head tip.
- Desktop-head is a strict superset of main (9 commits ahead, 0 behind).
- PR #38 already exists as DRAFT targeting main from dev/cnov/desktop-head.
- Pushing to desktop-head automatically updates PR #38 and triggers CI.
- Additional commits may land on ralph before merge (from parallel tasks .2 and .3), so tip SHA is not fixed.

## Acceptance
- [ ] Fast-forward ancestry check passes: `git merge-base --is-ancestor dev/cnov/desktop-head <ralph-tip>`
- [ ] `dev/cnov/desktop-head` tip matches ralph branch tip at merge time (verified via `git log -1`)
- [ ] `origin/dev/cnov/desktop-head` updated on remote
- [ ] PR #38 shows all commits from ralph branch
- [ ] CI triggered on PR #38

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
