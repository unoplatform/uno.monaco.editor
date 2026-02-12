# fn-11-extract-emitter-as-general-purpose-dts.5 Build test suite with generic .d.ts fixtures

## Description
Build the test suite for the standalone library. Unit tests for parser, emitter snapshot tests, end-to-end source generator integration tests, runtime converter validation, and real non-Monaco `.d.ts` fixtures. Monaco parity testing is in task 7.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp.Tests/DtsSharp.Tests.csproj` (new)
- `tools/DtsSharp/DtsSharp.Tests/ParserTests.cs` (new)
- `tools/DtsSharp/DtsSharp.Tests/EmitterTests.cs` (new — snapshot tests)
- `tools/DtsSharp/DtsSharp.Tests/GeneratorIntegrationTests.cs` (new — source generator tests via `CSharpGeneratorDriver`)
- `tools/DtsSharp/DtsSharp.Tests/RuntimeConverterTests.cs` (new)
- `tools/DtsSharp/DtsSharp.Tests/Fixtures/` (new — `.d.ts` test files)
- `tools/DtsSharp/DtsSharp.Tests/Snapshots/` (new — `.verified.cs` baselines)

## Approach

**Parser unit tests:**
- Test each declaration type: interfaces, classes, enums, type aliases, functions, namespaces
- Test type expression parsing: primitives, unions, intersections, arrays, generics, etc.
- Test JSDoc extraction, modifier handling, overload grouping

**Emitter snapshot tests:**
- Adapt `EmitterTestHelper.EmitToMemory()` and `SnapshotAssert` patterns from existing `MonacoTypeEmitter.Tests`
- Uses `.verified.cs`/`.received.cs` convention
- Test various `EmitterOptions` configurations (with/without converter type, doc link base URL, root namespace)

**Source generator integration tests:**
- Use `CSharpGeneratorDriver` to run `DtsSharpGenerator` against test `.d.ts` fixtures
- Verify correct source output is produced
- **Incremental caching test:** Run generator twice with same input → verify `GeneratorDriverRunResult` shows no re-emission on second run. Run with modified input → verify re-emission occurs.
- **Multiple `.d.ts` test:** Include 2 `.d.ts` files → verify independent generation

**Real library fixtures (3+ non-Monaco):**
- Actual `.d.ts` files from popular npm packages (e.g., trimmed subsets of `@types/lodash`, `@types/node`, a simple library)
- **Fixture requirements:** Pinned to specific versions, trimmed to relevant declarations only (not full files), include license attribution comment at top of each fixture
- End-to-end: `.d.ts` → parser → emitter → verified `.cs` for each fixture

**Runtime validation:**
- Emit C# → compile with `DtsSharp.Runtime` → verify `InterfaceToClassConverter` round-trips JSON

**NOT in this task:** Monaco parity (task 7).

## Key context

- SnapshotAssert uses `.verified.cs`/`.received.cs` convention
- Arrays sorted alphabetically for deterministic snapshots
- Line ending normalization for cross-platform
- `CSharpGeneratorDriver` is the standard way to unit test Roslyn source generators

## Acceptance
- [ ] `dotnet test --project tools/DtsSharp/DtsSharp.Tests` passes
- [ ] Parser unit tests cover: interfaces, classes, enums, type aliases, functions, namespaces, type expressions
- [ ] At least 3 real non-Monaco library `.d.ts` fixtures — pinned version, trimmed, with license attribution
- [ ] End-to-end: `.d.ts` → parser → emitter → verified `.cs` for each fixture
- [ ] Source generator integration tests via `CSharpGeneratorDriver`
- [ ] Incremental caching test: unchanged input doesn't trigger re-emission
- [ ] Multiple `.d.ts` files test: independent generation verified
- [ ] Runtime integration: generated code compiles with `DtsSharp.Runtime`, JSON round-trips
- [ ] Test project in `DtsSharp.slnx`

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
