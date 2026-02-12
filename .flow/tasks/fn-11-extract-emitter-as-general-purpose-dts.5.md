# fn-11-extract-emitter-as-general-purpose-dts.5 Build test suite with generic .d.ts fixtures

## Description
Build the test suite for the standalone library. Port relevant tests from `MonacoTypeEmitter.Tests/` and add new test fixtures using non-Monaco `.d.ts` files to validate general-purpose behavior.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp.Tests/DtsSharp.Tests.csproj` (new)
- `tools/DtsSharp/DtsSharp.Tests/ParserTests.cs` (new — unit tests for .d.ts parser)
- `tools/DtsSharp/DtsSharp.Tests/EmitterTests.cs` (new — snapshot tests for emitter)
- `tools/DtsSharp/DtsSharp.Tests/EndToEndTests.cs` (new — .d.ts → C# round-trip)
- `tools/DtsSharp/DtsSharp.Tests/Fixtures/` (new — test .d.ts files)
- `tools/DtsSharp/DtsSharp.Tests/Snapshots/` (new — verified snapshot outputs)

## Approach

**Port from existing tests:**
- Adapt `EmitterTestHelper.EmitToMemory()` pattern from `tools/MonacoTypeEmitter.Tests/EmitterTestHelper.cs`
- Adapt `SnapshotAssert` pattern from `tools/MonacoTypeEmitter.Tests/SnapshotAssert.cs`
- Port snapshot tests for individual type patterns (enum, interface, class, type alias)

**New generic fixtures (at least 3 non-Monaco libraries):**
- A small hand-written `.d.ts` covering all supported constructs (interfaces with generics, enums, type aliases, functions, nested namespaces)
- A subset of a real library's `.d.ts` (e.g., a simplified `chart.js` or `marked` declaration)
- A stress-test `.d.ts` with edge cases (deeply nested generics, long union types, exotic identifiers, optional/readonly combinations)

**Test levels:**
1. **Parser unit tests**: Input `.d.ts` snippet → expected `TypeModel` fragment
2. **Emitter snapshot tests**: Input `TypeModel` JSON → verified `.cs` output
3. **End-to-end tests**: Input `.d.ts` file → parser → emitter → verified `.cs` output
4. **Monaco parity test**: Parse `monaco.d.ts` → emit C# → compare against current verified output

## Key context

- Existing `SnapshotAssert` at `tools/MonacoTypeEmitter.Tests/SnapshotAssert.cs` uses a `.verified.cs`/`.received.cs` convention — follow the same pattern
- Sorting: all model arrays must be sorted alphabetically by name for deterministic snapshots
- Line ending normalization: tests must normalize CRLF/LF for cross-platform consistency
## Acceptance
- [ ] `dotnet test --project tools/DtsSharp/DtsSharp.Tests` passes all tests
- [ ] Parser unit tests cover: interfaces (with generics, extends), classes, enums, type aliases, functions, namespaces
- [ ] Parser unit tests cover type expressions: primitives, references, unions, intersections, arrays, tuples, literals, function types
- [ ] At least 3 non-Monaco `.d.ts` fixtures with verified snapshot output
- [ ] End-to-end test: `.d.ts` → parser → emitter → `.cs` verified
- [ ] Monaco parity: emitter output from parsed `monaco.d.ts` matches current verified baselines (modulo expected namespace/converter diffs)
- [ ] Test project added to `DtsSharp.slnx`
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
