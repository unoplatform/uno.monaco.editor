# fn-11-extract-emitter-as-general-purpose-dts.5 Build test suite with generic .d.ts fixtures

## Description
Build the test suite for the standalone library. Port relevant tests, add real non-Monaco library `.d.ts` fixtures, validate `DtsSharp.Runtime`, and verify library-level dual input (parser API + JSON deserialization). Monaco parity testing is in task 7.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp.Tests/DtsSharp.Tests.csproj` (new)
- `tools/DtsSharp/DtsSharp.Tests/ParserTests.cs` (new)
- `tools/DtsSharp/DtsSharp.Tests/EmitterTests.cs` (new — snapshot tests)
- `tools/DtsSharp/DtsSharp.Tests/EndToEndTests.cs` (new — .d.ts → C# round-trip)
- `tools/DtsSharp/DtsSharp.Tests/RuntimeConverterTests.cs` (new)
- `tools/DtsSharp/DtsSharp.Tests/DualInputTests.cs` (new — library-level dual input)
- `tools/DtsSharp/DtsSharp.Tests/Fixtures/` (new)
- `tools/DtsSharp/DtsSharp.Tests/Snapshots/` (new)

## Approach

**Port from existing tests:**
- Adapt `EmitterTestHelper.EmitToMemory()` and `SnapshotAssert` patterns

**Real library fixtures (3+ non-Monaco):**
- Actual `.d.ts` files from popular npm packages

**Library-level dual input test:**
- Parse a `.d.ts` fixture via `DtsParser.Parse()`
- Separately, serialize the resulting `TypeModel` to JSON, then deserialize back
- Emit C# from both models → output must be identical
- This proves the library's dual input contract (epic key design decision #2)

**Runtime validation:**
- Emit C# → compile with `DtsSharp.Runtime` → verify `InterfaceToClassConverter` round-trips JSON

**NOT in this task:** Monaco parity (task 7).

## Key context

- SnapshotAssert uses `.verified.cs`/`.received.cs` convention
- Arrays sorted alphabetically for deterministic snapshots
- Line ending normalization for cross-platform

## Acceptance
- [ ] `dotnet test --project tools/DtsSharp/DtsSharp.Tests` passes
- [ ] Parser unit tests cover: interfaces, classes, enums, type aliases, functions, namespaces, type expressions
- [ ] At least 3 real non-Monaco library `.d.ts` fixtures
- [ ] End-to-end: `.d.ts` → parser → emitter → verified `.cs` for each fixture
- [ ] Library dual input test: `DtsParser.Parse()` and JSON deserialization produce identical emitter output
- [ ] Runtime integration: generated code compiles with `DtsSharp.Runtime`, JSON round-trips
- [ ] Test project in `DtsSharp.slnx`

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
