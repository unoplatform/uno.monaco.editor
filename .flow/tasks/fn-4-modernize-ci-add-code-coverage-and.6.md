# fn-4-modernize-ci-add-code-coverage-and.6 Add generator pipeline tests

## Description
Add comprehensive tests for the new generator pipeline. This task is tests only — migration/cleanup is Task .7.

**Size:** M
**Files:** `tools/MonacoTypeEmitter.Tests/` (new test project)

**Depends on:** Tasks .4 and .5

## Approach

**Three test levels:**

1. **Snapshot tests** (Verify): Feed known `.d.ts` fragments to extractor -> intermediate JSON -> emitter -> compare C# output against `.verified.cs` snapshots. At least 5 cases: string enum, numeric enum, interface with properties, model class, namespace hierarchy.
   - Require deterministic ordering in both JSON and emitted files to prevent churn.

2. **Smoke test (full pipeline with serialization validation)**: Parse real `monaco.d.ts` -> intermediate JSON -> emit C# -> build emitted files in an isolated temp project that references `InterfaceToClassConverter`, `MonacoJsonContext` patterns -> run a minimal serialization contract subset against emitted types. This goes beyond "does it compile?" to validate JSON round-trip fidelity.

3. **Round-trip tests**: For key types (MarkerData, CompletionItem, CursorStyle, MarkerSeverity), generate C# from the intermediate model, then verify serialization output matches the existing golden baselines in `SerializationContractTests.cs`.

## Key context

- The smoke test is the critical one — it would have caught the TypedocConverter breakage immediately.
- Verify (snapshot testing) integrates with xunit.v3 and supports net10.0.
- The isolated temp project for the smoke test ensures emitted code compiles in a realistic context (with real converters/serialization), not just syntactically.
- `SerializationContractTests.cs` (1218 lines, ~40 tests) already validates wire format for all major types — this is the correctness baseline for round-trip tests.

## Acceptance
- [ ] `tools/MonacoTypeEmitter.Tests/` project exists, added to solution
- [ ] Snapshot tests: at least 5 cases (string enum, numeric enum, interface, model class, namespace) with `.verified.cs` files
- [ ] Smoke test: full pipeline (parse -> emit -> compile -> serialization contract subset passes)
- [ ] Smoke test compiles emitted files in isolated project with real converter/context references (not just syntax check)
- [ ] Round-trip tests: key types (MarkerData, CompletionItem, CursorStyle, MarkerSeverity) serialize identically to existing golden baselines
- [ ] Deterministic ordering verified: re-running generator produces identical output
- [ ] All existing tests still pass (`dotnet test MonacoEditorComponent.Tests`)

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
