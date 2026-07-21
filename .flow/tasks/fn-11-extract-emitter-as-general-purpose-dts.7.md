# fn-11-extract-emitter-as-general-purpose-dts.7 Harden parser: edge constructs and Monaco .d.ts parity

## Description
Harden the .d.ts parser for edge constructs and achieve zero-diff parity with the ts-morph extractor on `monaco.d.ts`. This task owns ALL Monaco parity testing and must resolve all diffs before task 6 can verify byte-for-byte identical source generator output. This is the highest-uncertainty task in the epic.

**Size:** L
**Files:**
- `tools/DtsSharp/DtsSharp/Parser/TypeExpressionParser.cs` (modify)
- `tools/DtsSharp/DtsSharp/Parser/DeclarationParser.cs` (modify)

## Approach

**Phase 1 — Parity harness:**
- Create a diff comparison script/test: `DtsParser.Parse(monaco.d.ts)` → emit C# with Monaco-equivalent `EmitterOptions` → diff against ts-morph baseline (current generated files)
- Classify all diffs by category (parser bug, ordering, whitespace, missing construct, etc.)

**Phase 2 — Edge constructs:**
- Type parameter defaults (`<T = string>`)
- `typeof` query nodes, `keyof` operator, indexed access types
- `ReadonlyArray<T>` normalization
- Named tuple elements, rest elements
- Heritage: `extends` (multiple for interfaces), `extends` + `implements` for classes
- Overloaded namespace functions
- `export default` / `export =`
- Empty interface/class bodies, string enum patterns

**Phase 3 — Fallback rules:**
- Mapped types → `objectLiteral` TypeInfo (empty)
- Template literals → `primitive` name `string`
- Conditional types → `conditional` TypeInfo
- `infer` → `primitive` name `unknown`

**Phase 4 — Fix batches:**
- Work through diff categories systematically until zero diffs remain
- **Stop/go gate:** If after edge constructs + fallbacks, more than 50 unique diff patterns remain, reassess approach before continuing

**Monaco parity (this task's gate — must reach zero diffs):**
- Parse `monaco.d.ts` via `DtsParser.Parse()` → emit C# with Monaco-equivalent `EmitterOptions` → diff against ts-morph baseline
- Fix ALL differences. No "acceptable diffs" — task 6 requires byte-for-byte parity.
- If a diff cannot be eliminated, it represents a parser bug that must be fixed.

## Key context

- ts-morph extractor at `tools/monaco-type-extractor/src/extractor.ts` handles 20+ type variants
- `model.json` from ts-morph is ground truth for the intermediate model
- Construct signatures are out of scope (not in model)
- The parity comparison is: `DtsParser.Parse(monaco.d.ts)` → `CSharpEmitter` with Monaco options → diff against current `MonacoEditorComponent/Monaco/*.cs` files

## Acceptance
- [ ] Parity harness: automated diff comparison between parser output and ts-morph baseline
- [ ] Parser handles: `typeof`, `keyof`, indexed access, `ReadonlyArray`, named tuples, rest, type parameter defaults, heritage clauses, overloaded namespace functions, `export default`/`export =`
- [ ] Fallback rules for: mapped types, template literals, conditional types, `infer`
- [ ] `monaco.d.ts` parses without errors
- [ ] Emitter output from parsed `monaco.d.ts` is identical to ts-morph extractor baseline (zero diffs)
- [ ] Parser changes target `netstandard2.0` — no ns2.1+ APIs

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
