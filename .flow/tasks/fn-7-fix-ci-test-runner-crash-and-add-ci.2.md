# fn-7-fix-ci-test-runner-crash-and-add-ci.2 Add CI verification policy to AGENTS.md

## Description
Add a CI verification policy section to AGENTS.md requiring agents to verify CI is green on active PRs before marking epics/tasks as done. This prevents leaving PRs in a broken CI state.

**Size:** S
**Files:** `AGENTS.md`

## Approach

Add a new section "## CI Verification Policy" after the "Development Workflow" section in `AGENTS.md` (after line 75). Follow existing AGENTS.md style (hierarchical markdown with code blocks).

The section should cover:
- **When it applies**: whenever a branch has an active PR open
- **What to do**: push changes, monitor CI via `gh pr checks <PR#> --watch`, fix failures, iterate until green
- **Timing**: at minimum before marking an epic done; ideally after significant pushes
- **How to check**: `gh pr checks` command, GitHub Actions UI
- **Policy**: never leave a PR in a broken CI state — fix before moving on

Also add to the "Development Workflow" validation checklist:
- Step 4: If branch has an active PR, verify CI passes after pushing

## Key context

- Current AGENTS.md has "Development Workflow" section (lines 67-75) with local validation checklist but NO CI verification requirement
- The CI pipeline has Build (ubuntu), Build macOS ARM, Desktop Tests (Windows), and Coverage Report jobs
- Desktop CDP tests are excluded from CI (require GUI) — document this known limitation
- `gh pr checks` is the preferred CLI command for monitoring
## Acceptance
- [ ] AGENTS.md contains new "CI Verification Policy" section
- [ ] Policy clearly states: branches with active PRs must have CI verified green before marking work done
- [ ] Includes `gh pr checks` command example
- [ ] Explains the CI job structure (Build, Build macOS ARM, Desktop Tests, Coverage Report)
- [ ] Notes known CI limitations (Desktop CDP tests excluded on headless runners)
- [ ] Development Workflow checklist updated to include CI verification step
- [ ] CI still passes after AGENTS.md update is pushed
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
