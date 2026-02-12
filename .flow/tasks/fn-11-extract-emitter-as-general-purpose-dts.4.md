# fn-11-extract-emitter-as-general-purpose-dts.4 Build .d.ts declaration parser in C# — core grammar

## Description
Build the core C# parser for TypeScript `.d.ts` declaration files — lexer, recursive-descent parser for top-level declarations, and type expression parser. Core grammar only; edge constructs and Monaco-parity hardening are in task 7.

**Size:** M
**Files:**
- `tools/DtsSharp/DtsSharp/Parser/DtsParser.cs` (new — main entry point)
- `tools/DtsSharp/DtsSharp/Parser/DtsLexer.cs` (new — tokenizer)
- `tools/DtsSharp/DtsSharp/Parser/TypeExpressionParser.cs` (new — type expressions)
- `tools/DtsSharp/DtsSharp/Parser/DeclarationParser.cs` (new — declarations)

## Approach

**Architecture:**
- `DtsLexer`: Hand-written lexer → token stream
- `DeclarationParser`: Recursive descent for declarations → model types
- `TypeExpressionParser`: Recursive descent for type expressions → `TypeInfo` variants
- `DtsParser`: Orchestrator → `TypeModel`

**Core grammar (this task ONLY):**
- Top-level: `declare namespace`, `declare module`, `interface`, `class`, `enum`, `const enum`, `type` aliases, `function`
- Members: properties (optional `?`, readonly), methods (overloads grouped by name), index signatures, call signatures
- Type expressions: primitives, references (with generics), unions, intersections, arrays (`T[]`, `Array<T>`), tuples, string/number literals, function types, object literals, parenthesized types
- Modifiers: `readonly`, `optional`, `export`, `declare`, `static`, `abstract`
- Generic type parameters with `extends` constraints
- JSDoc extraction (`/** ... */` → description, `@param` tags)

**NOT in this task (deferred to task 7):**
- Type parameter defaults, `typeof`, `keyof`, indexed access, `ReadonlyArray` normalization, named tuples, rest elements, multi-extends heritage, `export default`/`export =`, conditional/mapped/template literal types, `infer`, Monaco parity

**Note:** Construct signatures are out of scope for the entire epic (not in the intermediate model).

**Fallback:** Unsupported constructs → `TypeInfo` kind `primitive` name `unknown` (no exceptions, log warning).

**Sorting:** All output arrays sorted alphabetically by name.

## Key context

- Model classes in `DtsSharp.Model` (from task 1). Parser produces instances of these.
- `TypeInfo` has 12 variants discriminated by `kind`.
- String literal union type aliases (`type Foo = "a" | "b"`) → recognized as string enum pattern.

## Acceptance
- [ ] `DtsParser.Parse(string dtsContent)` returns valid `TypeModel`
- [ ] Parses `declare namespace` with nested interfaces, classes, enums, type aliases, functions
- [ ] Parses members: properties (optional, readonly), methods (with overloads), index signatures, call signatures
- [ ] Parses type expressions: primitives, references, unions, intersections, arrays, tuples, literals, function types, object literals, parenthesized types
- [ ] Parses generic type parameters with `extends` constraints
- [ ] Extracts JSDoc comments (description + `@param` tags)
- [ ] Handles `export` and `declare` modifiers
- [ ] Unsupported constructs → `unknown` primitive TypeInfo (no exceptions)
- [ ] Output arrays sorted alphabetically
- [ ] Pure C# — no Node.js
- [ ] Smoke test: parses hand-written multi-construct `.d.ts` fixture

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
