## Description
Decide and document the XML documentation strategy for the ~40+ generated Monaco type files in `MonacoEditorComponent/Monaco/`. This task requires fn-4 epic to be complete so the emitter output is stable.

**Size:** S
**Files:** `docs/generated-type-docs-strategy.md`

## Approach

1. **Baseline the fn-4.5 emitter output**: Inventory all generated `.cs` files, count public symbols, assess existing doc coverage
2. **Evaluate strategies**:
   - **Strategy A — Emitter generates XML docs**: Modify emitter to emit XML docs from TypeScript JSDoc in `monaco.d.ts`. Keeps docs in sync automatically.
   - **Strategy B — Post-processing step**: Separate tool merges hand-written XML docs with emitter output after regeneration.
   - **Strategy C — Hand-written with preservation**: Add XML docs manually with convention (e.g., separate `.xmldoc` files) surviving regeneration.
3. **Decision criteria**: Maintenance burden, doc quality, JSDoc coverage in `monaco.d.ts`, regeneration frequency
4. **Document chosen strategy** with rationale, implementation steps, and acceptance criteria for task 8

## Key context
- **Gate condition**: fn-4 epic must be complete (emitter output pinned) before this task starts
- `StandaloneEditorConstructionOptions` already has 119 summary doc entries — assess what emitter preserves
- Core types needing thorough docs: `Position`, `Range`, `Selection`, `IPosition`, `IRange`, `Uri`
- ~35+ enums, ~40+ classes/interfaces in `Monaco/Editor/` and `Monaco/Languages/`

## Acceptance
- [ ] Strategy document exists at `docs/generated-type-docs-strategy.md`
- [ ] fn-4.5 emitter output baselined (file count, public symbol count, existing doc coverage)
- [ ] At least 2 strategies evaluated with pros/cons
- [ ] Chosen strategy documented with rationale
- [ ] Implementation steps for task 8 clearly defined
- [ ] Acceptance criteria for task 8 derived from chosen strategy
