# Fix CI test runner crash and add CI verification policy

## Overview

PR #38 (`dev/cnov/desktop-head` → `main`) has failing CI on all platforms. The test runner crashes with a `TypeLoadException` before any tests execute. Additionally, AGENTS.md lacks a CI verification policy — agents must be required to verify CI green on active PRs before marking work done.

## Root Cause

`Microsoft.Testing.Extensions.CodeCoverage` v18.0.4 expects `Microsoft.Testing.Platform` v2.0.4+ interfaces, but `Microsoft.NET.Test.Sdk` v18.0.1 resolves `Microsoft.Testing.Platform` v2.0.1 — the interface mismatch causes:

```
System.TypeLoadException: Method 'OnTestSessionStartingAsync' in type
'Microsoft.Testing.Extensions.CodeCoverage.TestingPlatformCoverageDynamicTestSessionLifetimeHandler'
does not have an implementation.
```

Both the Ubuntu (`Build`) and macOS ARM (`Build (macOS ARM)`) jobs fail at the "Run tests" step. `Desktop Tests (Windows)` is skipped (depends on `Build`). `Coverage Report` fails downstream (no coverage data).

## Fix Approach

Update test infrastructure packages to latest versions and remove the redundant `Microsoft.NET.Test.Sdk` (not needed with `xunit.v3.mtp-v2`):

| Package | Current | Target |
|---------|---------|--------|
| `xunit.v3.mtp-v2` | 3.2.0 | 3.2.2 |
| `Microsoft.Testing.Extensions.CodeCoverage` | 18.0.4 | 18.4.1 |
| `Microsoft.NET.Test.Sdk` | 18.0.1 | **REMOVE** |
| `Microsoft.Testing.Extensions.TrxReport` | (missing) | 2.0.2 |

`xunit.v3.mtp-v2` bundles Microsoft Testing Platform v2 — making `Microsoft.NET.Test.Sdk` redundant and the source of the version conflict. TrxReport is added for structured test result output.

## Quick commands

```bash
# Verify fix locally before pushing
dotnet restore MonacoEditorComponent.slnx
dotnet build MonacoEditorComponent.slnx -c Release --no-restore
dotnet test --project MonacoEditorComponent.Tests/MonacoEditorComponent.Tests.csproj -c Release --no-build -- --filter-not-trait "Category=DesktopCDP"
dotnet test --project tools/MonacoTypeEmitter.Tests/MonacoTypeEmitter.Tests.csproj

# Monitor CI after push
gh pr checks <PR#> --watch
```

## Acceptance

- [ ] All CI build/test jobs pass (Build, Build macOS ARM, Desktop Tests Windows)
- [ ] Coverage Report job succeeds with merged coverage data
- [ ] `dotnet test` passes locally on both test projects
- [ ] AGENTS.md contains CI verification policy section
- [ ] No regressions in existing test behavior
