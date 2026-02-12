# fn-11-extract-emitter-as-general-purpose-dts.7 Harden parser: edge constructs and Monaco .d.ts parity

## Description
Harden the .d.ts parser for edge constructs, achieve zero-diff parity with ts-morph extractor on `monaco.d.ts`, and wire the completed parser into the CLI. This task owns ALL Monaco parity testing and must resolve all diffs before task 6 can verify byte-for-byte identical output.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp/Parser/TypeExpressionParser.cs` (modify)
- `tools/DtsSharp/DtsSharp/Parser/DeclarationParser.cs` (modify)
- `tools/DtsSharp/DtsSharp.Cli/Program.cs` (modify — wire parser, remove stub)

## Approach

**Edge constructs (deferred from task 4):**
- Type parameter defaults (`<T = string>`)
- `typeof` query nodes, `keyof` operator, indexed access types
- `ReadonlyArray<T>` normalization
- Named tuple elements, rest elements
- Heritage: `extends` (multiple for interfaces), `extends` + `implements` for classes
- Overloaded namespace functions
- `export default` / `export =`
- Empty interface/class bodies, string enum patterns

**Fallback rules:**
- Mapped types → `objectLiteral` TypeInfo (empty)
- Template literals → `primitive` name `string`
- Conditional types → `conditional` TypeInfo
- `infer` → `primitive` name `unknown`

**Monaco parity (this task's gate — must reach zero diffs):**
- Parse `monaco.d.ts` → emit C# with Monaco options → diff against ts-morph baseline
- Fix ALL differences. No "acceptable diffs" — task 6 requires byte-for-byte parity.
- If a diff cannot be eliminated, it represents a parser bug that must be fixed.

**Wire into CLI:**
- Remove `NotImplementedException` from `DtsSharp.Cli/Program.cs`
- Wire `DtsParser.Parse()` into `.d.ts` input path

## Key context

- ts-morph extractor at `tools/monaco-type-extractor/src/extractor.ts` handles 20+ type variants
- `model.json` from ts-morph is ground truth
- Construct signatures are out of scope (not in model)

## Acceptance
- [ ] Parser handles: `typeof`, `keyof`, indexed access, `ReadonlyArray`, named tuples, rest, type parameter defaults, heritage clauses, overloaded namespace functions, `export default`/`export =`
- [ ] Fallback rules for: mapped types, template literals, conditional types, `infer`
- [ ] `monaco.d.ts` parses without errors
- [ ] Emitter output from parsed `monaco.d.ts` is identical to ts-morph extractor baseline (zero diffs)
- [ ] CLI `.d.ts` input path wired — no more stub
- [ ] `dotnet run --project tools/DtsSharp/DtsSharp.Cli -- --input monaco.d.ts --output /tmp/test/` works

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
