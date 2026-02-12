# fn-6-bug-fixes-disposal-resize-and-test.6 Configure coverage reporting, fill gaps to 85 percent, and close bug ledger

## Description

**Size**: M — coverage tooling setup + targeted test additions + changelog

**Problem**: Need to reach 85%+ line coverage on the `MonacoEditorComponent` assembly and close out the bug ledger with changelog entries.

**Files**:
- `MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj` — add coverage packages/config
- `MonacoEditorComponent.Tests/` — new test files for uncovered paths
- `changelog.md` — bug fix entries
- `.flow/specs/fn-6-bug-fixes-disposal-resize-and-test.md` — update bug inventory status

**Approach**:
1. **Configure coverage tooling**:
   - Ensure `Microsoft.Testing.Extensions.CodeCoverage` is referenced in test csproj
   - Configure coverage output format (Cobertura XML)
   - Coverage scope: `MonacoEditorComponent` assembly only (exclude test project, test app)
   - Target metric: **line coverage** on `net10.0-desktop` TFM

2. **Measure baseline**:
   - Run coverage collection: `dotnet test --project ... -- --coverage --coverage-output-format cobertura`
   - Generate report to identify uncovered code paths

3. **Fill coverage gaps** (prioritize by risk):
   - Untested public API methods on `CodeEditor`
   - Error handling branches
   - Platform-specific paths (WASM vs Desktop presenters)
   - Bridge/interop code paths
   - Extension methods
   - Mark genuinely untestable interop paths with `[ExcludeFromCodeCoverage]` + XML doc justification

4. **Close bug ledger**:
   - Update `changelog.md` with entries for each fixed bug
   - Document deferred bugs (BUG 5) with rationale
   - Update epic bug inventory table with final status

**Key context**:
- `Microsoft.Testing.Extensions.CodeCoverage` is the correct tool for xUnit v3 (.NET 10) — not coverlet
- Coverage metric: **line coverage** (not branch, not method)
- Coverage TFM: `net10.0-desktop` (most code paths reachable without browser runtime)
- `[ExcludeFromCodeCoverage]` requires documented justification per usage (e.g., "JSImport interop — requires browser runtime")
- Don't pad coverage with trivial tests; focus on meaningful assertions

## Acceptance
- [ ] Coverage tooling configured and working
- [ ] Baseline coverage report generated
- [ ] Line coverage >= 85% on `MonacoEditorComponent` assembly (net10.0-desktop)
- [ ] All `[ExcludeFromCodeCoverage]` usages have documented justification
- [ ] Coverage gaps filled with meaningful tests (not trivial/padding)
- [ ] `dotnet test` passes with coverage collection enabled
- [ ] Coverage report artifacts generated (Cobertura XML)
- [ ] `changelog.md` updated with all bug fix entries
- [ ] Deferred bugs documented with rationale
- [ ] Epic bug inventory table updated with final status

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
