# fn-11-extract-emitter-as-general-purpose-dts.4 Build .d.ts declaration parser in C#

## Description
Build a C# parser for TypeScript `.d.ts` declaration files that produces the same intermediate `TypeModel` the emitter consumes. This eliminates the Node.js/ts-morph dependency for the common case.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp/Parser/DtsParser.cs` (new — main entry point)
- `tools/DtsSharp/DtsSharp/Parser/DtsLexer.cs` (new — tokenizer)
- `tools/DtsSharp/DtsSharp/Parser/TypeExpressionParser.cs` (new — parse type expressions)
- `tools/DtsSharp/DtsSharp/Parser/DeclarationParser.cs` (new — parse interface/class/enum/function declarations)

## Approach

**Scope for v1 — the `.d.ts` declaration subset:**
- `.d.ts` files contain ONLY declarations (no implementation code), making this a bounded grammar
- The parser must handle: `declare namespace`, `declare module`, `interface`, `class`, `enum`, `const enum`, `type` aliases, `function` declarations
- Members: properties, methods (with overloads), index signatures, call signatures, construct signatures
- Type expressions: primitives (`string`, `number`, `boolean`, `void`, `null`, `undefined`, `any`, `unknown`, `never`, `object`, `symbol`, `bigint`), type references (with generic args), unions (`A | B`), intersections (`A & B`), arrays (`T[]`, `Array<T>`, `ReadonlyArray<T>`), tuples (`[A, B]`), string/number literals, function types (`(a: T) => U`), object literals (`{ key: T }`), `typeof`, `keyof`, indexed access (`T[K]`)
- Modifiers: `readonly`, `optional` (`?`), `export`, `declare`, `static`, `abstract`
- JSDoc comment extraction (`/** ... */` → description, `@param` tags)
- Generic type parameters with constraints (`<T extends Base>`)

**What to defer (not in v1):**
- Declaration merging across multiple files (v1 = single file)
- `/// <reference>` directive following
- Conditional types (`T extends U ? A : B`) — emit as `TypeInfo` with kind `conditional`, let emitter handle
- Mapped types (`{ [K in keyof T]: V }`) — emit as object literal
- Template literal types (`` `hello ${T}` ``) — emit as `string` primitive

**Architecture:**
- `DtsLexer`: Hand-written lexer (not regex-based). TypeScript tokens are well-defined. Produces token stream.
- `DeclarationParser`: Recursive descent parser for top-level declarations. Produces `TypeNamespace`, `InterfaceInfo`, `ClassInfo`, `EnumInfo`, `TypeAliasInfo`, `FunctionInfo`.
- `TypeExpressionParser`: Recursive descent parser for type expressions. Produces `TypeInfo` discriminated union.
- `DtsParser`: Orchestrator — reads file, lexes, parses, returns `TypeModel`.

**Validation approach:** Parse `monaco.d.ts` and compare output to the ts-morph extractor's `model.json`. The delta reveals what the parser misses.

## Key context

- The existing intermediate model at `tools/monaco-type-extractor/src/model.ts` has 12 `TypeInfo` variants. The parser must produce the same discriminated union.
- Method overloads: TS allows multiple signatures for the same method name. Group them by name, produce `MethodOverload[]` on `MethodInfo`.
- String literal union type aliases (`type Foo = "a" | "b" | "c"`) are emitted as string enums by the emitter. The parser must recognize this pattern.
- The extractor sorts all output arrays alphabetically by name for deterministic snapshot testing. The parser must do the same.
## Acceptance
- [ ] `DtsParser.Parse(string dtsContent)` returns a valid `TypeModel`
- [ ] Parses `declare namespace` with nested interfaces, classes, enums, type aliases, functions
- [ ] Parses interface members: properties (optional, readonly), methods (with overloads), index signatures, call signatures
- [ ] Parses type expressions: primitives, references (with generics), unions, intersections, arrays, tuples, string/number literals, function types, object literals
- [ ] Parses generic type parameters with `extends` constraints
- [ ] Extracts JSDoc comments (description + `@param` tags)
- [ ] Handles `export` and `declare` modifiers
- [ ] Output arrays sorted alphabetically by name (deterministic)
- [ ] Passes round-trip test: parse `monaco.d.ts` → emit C# → diff against current emitter output shows only expected differences
- [ ] No Node.js dependency — pure C# implementation
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
