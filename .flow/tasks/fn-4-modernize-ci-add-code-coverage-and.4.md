# fn-4-modernize-ci-add-code-coverage-and.4 Create ts-morph Monaco type extractor (Node.js parser)

## Description
Create a Node.js tool using ts-morph that parses `monaco.d.ts` and produces a versioned intermediate JSON model. This replaces the broken TypeDoc 0.20.37 + TypedocConverter pipeline.

**Size:** M
**Files:** `tools/monaco-type-extractor/` (new): `package.json`, `tsconfig.json`, `src/index.ts`, `src/extractor.ts`, `src/model.ts`

## Approach

- Use ts-morph (v27+) which bundles its own TS compiler and handles TS 5.x natively
- **Execution:** `npx tsx src/index.ts` for dev, `npm run build && node dist/index.js` for CI. Define in `package.json` scripts and use consistently everywhere.
- Parse `monaco.d.ts` via `project.addSourceFileAtPath()`
- Walk nested `declare namespace monaco { ... }` blocks
- Extract into a versioned intermediate JSON schema (`schemaVersion` field)

**Schema must capture (beyond basic types):**
- Interfaces: properties (name, structured type info, optional, readonly), methods (name, parameters, return type, overloads), inheritance chain, generic type parameters
- Enums: members with values, distinguish string-backed vs numeric
- Type aliases: resolve to underlying type, handle literal unions (-> string enums)
- Union/intersection types: decomposed structurally (not just type text)
- Index signatures, callable types, readonly modifiers
- Namespace hierarchy (`monaco.editor`, `monaco.languages`)
- JSDoc comments

**Deterministic ordering:** All output arrays sorted alphabetically by name (namespaces -> types -> members -> properties). This prevents snapshot churn.

## Key context

- ts-morph v27 bundles its own TypeScript compiler — no version conflicts with the project's root TS version
- Monaco's `monaco.d.ts` uses nested `declare namespace monaco { ... }` blocks — extractor must walk into module declarations
- Current pipeline extracts ~100+ interfaces and ~30+ enums from Monaco's API surface
- The intermediate model must distinguish string-backed enums (need `JsonStringEnumConverter`) from numeric enums (no converter) — this is a critical distinction for the .NET emitter
- The JSON schema is the contract between this tool and the .NET emitter — it must be versioned and documented
- Reference real-world ts-morph parsers: `elastic/elasticsearch-specification`, `lynx-family/lynx`, `RoryDuncan/ts2cs`

## Acceptance
- [ ] `tools/monaco-type-extractor/` exists with `package.json` (tsx + ts-morph deps), `tsconfig.json`, TypeScript source
- [ ] Execution works via `npx tsx src/index.ts <path>` (same command in package.json scripts, CLI, and CI)
- [ ] Extractor parses current `monaco.d.ts` (TS 5.x) without errors
- [ ] Intermediate JSON has `schemaVersion` field for forward compatibility
- [ ] JSON captures: interfaces (properties with structured type info, methods with overloads, inheritance), enums (string vs numeric distinguished), type aliases, union/intersection decomposition, index signatures, namespaces
- [ ] All output arrays sorted alphabetically (deterministic ordering)
- [ ] Schema documented as TypeScript types in `src/model.ts`

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
