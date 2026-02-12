# fn-6-bug-fixes-disposal-resize-and-test.6 Configure coverage reporting, fill gaps to 85 percent, and close bug ledger

## Description

**Size**: M — coverage tooling setup + targeted test additions + changelog

**Problem**: Need to reach 85%+ line coverage on the `MonacoEditorComponent` assembly and close out the bug ledger with changelog entries.

**Files**:
- `MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj` — coverage config (already has `Microsoft.Testing.Extensions.CodeCoverage`)
- `MonacoEditorComponent.Tests/` — new test files for uncovered paths
- `changelog.md` — bug fix entries
- `.flow/specs/fn-6-bug-fixes-disposal-resize-and-test.md` — update bug inventory status

**Approach**:
1. **Configure coverage reporting**:
   - `Microsoft.Testing.Extensions.CodeCoverage` already referenced in test csproj
   - Coverage command (tests target `net10.0`): `dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj -- --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml`
   - Coverage scope: `MonacoEditorComponent` assembly only (exclude test project, test app)
   - Target metric: **line coverage** on `net10.0` TFM (the test project TFM)

2. **Measure baseline**:
   - Run coverage collection
   - Generate report to identify uncovered code paths (use `reportgenerator` or equivalent)

3. **Fill coverage gaps** (prioritize by risk):
   - Untested public API methods on `CodeEditor`
   - Error handling branches
   - Platform-specific paths (WASM vs Desktop presenters)
   - Bridge/interop code paths
   - Extension methods
   - Mark genuinely untestable interop paths with `[ExcludeFromCodeCoverage]` + XML doc justification

4. **Close bug ledger** (31 bugs total):
   - Update `changelog.md` with entries for each fixed bug (BUGs 1-4, 6-12, 14-31)
   - Document deferred bugs (BUG 5: AllowedFileContentRoot) with rationale
   - Document already-verified bugs (BUG 13: sanitize/desanitize) with rationale
   - Verify every bug (1-31) has at least one regression test in the test suite
   - Update epic bug inventory table with final status (Fixed/Deferred/Verified)

**Key context**:
- Tests target `net10.0` (not a platform-specific TFM) — see `MonacoEditorComponent.Tests.csproj:4`
- `Microsoft.Testing.Extensions.CodeCoverage` is already in the csproj
- Coverage metric: **line coverage** (not branch, not method)
- `[ExcludeFromCodeCoverage]` requires documented justification per usage (e.g., "JSImport interop — requires browser runtime")
- Don't pad coverage with trivial tests; focus on meaningful assertions

## Acceptance
- [ ] Coverage reporting configured and working
- [ ] Baseline coverage report generated
- [ ] Line coverage >= 85% on `MonacoEditorComponent` assembly (`net10.0` TFM)
- [ ] All `[ExcludeFromCodeCoverage]` usages have documented justification
- [ ] Coverage gaps filled with meaningful tests (not trivial/padding)
- [ ] `dotnet test` passes with coverage collection enabled
- [ ] Coverage report artifacts generated (Cobertura XML)
- [ ] `changelog.md` updated with all 31 bug fix entries
- [ ] Every bug (1-31) has at least one regression test verified
- [ ] Deferred bugs (BUG 5) documented with rationale
- [ ] Verified bugs (BUG 13) documented with rationale
- [ ] Epic bug inventory table updated with final status (Fixed/Deferred/Verified)

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
