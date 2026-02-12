# XML Documentation Strategy for Generated Monaco Types

## Context

The `uno.monaco.editor` project wraps the Monaco Editor (v0.52.2) in a Uno Platform
control. The `MonacoEditorComponent/Monaco/` directory contains C# types that mirror
Monaco's TypeScript API. These types are produced by a two-stage pipeline:

1. **TypeScript extractor** (`tools/monaco-type-extractor/`) -- parses `monaco.d.ts` via
   ts-morph, producing an intermediate JSON model with full JSDoc documentation.
2. **C# emitter** (`tools/MonacoTypeEmitter/`) -- reads the JSON model and emits C#
   source files into `MonacoEditorComponent/Monaco/`.

This document defines the strategy for adding comprehensive XML documentation to all
generated Monaco types, satisfying the fn-5.8 implementation task.

## Baseline: fn-4.5 Emitter Output

### File inventory

| Location | Files | Category |
|----------|------:|----------|
| `Monaco/Editor/` | 69 | Editor interfaces, concrete classes, enums |
| `Monaco/Languages/` | 26 | Language provider types |
| `Monaco/Helpers/` | 6 | CSS style helpers (hand-written) |
| `Monaco/` (root) | 16 | Core types (IPosition, IRange, Uri, KeyCode, etc.) |
| **Total** | **117** | |

### Public symbol counts

| Symbol type | Count |
|-------------|------:|
| Public type declarations (class, interface, enum, static class) | 67 |
| Public properties and members | ~398 |
| Existing `<summary>` tags | 570 |

### Existing documentation coverage

The emitter already emits `<summary>` XML doc comments when JSDoc is present in the
TypeScript source. The `WriteDocComment()` method in `CSharpEmitter.cs` writes JSDoc
content as `<summary>` tags for types, properties, methods, and call signatures.
However, **enum member documentation is not yet emitted** -- the `WriteDocComment()`
call site for individual enum members is missing. This means type-level, property-level,
and method-level docs flow through today, but enum members only get their names.

Files with strong existing coverage:
- `IEditorOptions.cs` -- 105 summary tags (matches `IEditorOptions` JSDoc in `monaco.d.ts`)
- `ISuggestOptions.cs` -- 35 summary tags
- `StandaloneEditorConstructionOptions.cs` -- 119 summary tags (hand-written, not generated)
- `IModelDecorationOptions.cs` -- 16 summary tags
- `CompletionItem.cs` -- 15 summary tags

Files with minimal or no coverage (typically enums with undocumented members):
- Most string-backed enums (1 summary tag = type-level only, no member docs)
- `IEditorFindOptions.cs` -- 1 summary tag
- `IEditorConstructionOptions.cs` -- 1 summary tag

### Source JSDoc richness

The upstream `monaco.d.ts` (v0.52.2) contains **1,434 JSDoc comments** across 9,337
lines. Documentation quality is high -- most interfaces and their properties have
descriptive JSDoc with defaults noted, behavioral explanations, and cross-references.

## Strategies Evaluated

### Strategy A: Enhance Emitter to Generate Richer XML Docs (RECOMMENDED)

**Description:** Modify the `CSharpEmitter` to produce richer XML documentation from
the already-extracted JSDoc data in the intermediate model. The extractor already
captures `documentation` fields at every level (types, properties, methods, parameters,
enum members). The emitter already writes `<summary>` tags. This strategy extends it.

**Changes required:**
1. Emit `<param>` tags from `ParameterInfo.Documentation` (already extracted)
2. Emit `<returns>` tags from method/function return type context
3. Emit `<remarks>` with Monaco TypeDoc cross-reference links
4. Emit enum member `<summary>` tags from `EnumMemberInfo.Documentation`
5. Emit `<see href="..."/>` links to Monaco TypeDoc API where applicable
6. Escape XML entities in documentation text (already implemented via `EscapeXml()`)

**Pros:**
- Docs stay in sync automatically on regeneration -- zero maintenance burden
- Leverages the 1,434 JSDoc comments already present in `monaco.d.ts`
- No separate tooling or post-processing step needed
- The extraction pipeline already captures per-parameter `@param` tags via
  `getParameterDocumentation()` in the extractor
- Incremental: can be done in stages (summary first, then param/returns/remarks)
- Consistent quality across all generated files

**Cons:**
- Upstream JSDoc gaps propagate to C# docs (some enums have no member docs)
- JSDoc phrasing may not perfectly match C#/.NET conventions (e.g., "truthy"
  language, JS-specific defaults)
- Cannot add C#-specific notes (like `PlatformNotSupportedException` behavior) to
  generated files without a supplementary mechanism

**Maintenance burden:** Very low. Regeneration automatically picks up upstream JSDoc
improvements. The emitter change is a one-time enhancement.

### Strategy B: Post-Processing Merge Step

**Description:** Add a separate tool that runs after the emitter, merging hand-written
XML doc overrides from `.xmldoc` sidecar files with the emitter output.

**Changes required:**
1. Define a sidecar file format (e.g., `IEditorOptions.xmldoc` alongside
   `IEditorOptions.cs`)
2. Build a merge tool that reads both files and produces the final output
3. Integrate into the regeneration pipeline

**Pros:**
- Full control over documentation content
- Can add C#-specific notes and platform-specific caveats
- Hand-written docs survive regeneration

**Cons:**
- Significant tooling overhead (new tool, new file format, new pipeline step)
- Manual maintenance burden: every upstream API change requires sidecar updates
- Risk of sidecar files drifting out of sync with generated types
- No automation benefit -- manually writing docs for ~400 members is substantial
- Duplicates the work the emitter could do from existing JSDoc

**Maintenance burden:** High. Each Monaco version upgrade requires reviewing and
updating sidecar files for any API changes.

### Strategy C: Hand-Written Documentation with Preservation

**Description:** Manually add XML documentation directly to the generated C# files,
with a convention (e.g., `// <hand-doc>` markers) that the emitter preserves during
regeneration.

**Changes required:**
1. Define marker convention for hand-written doc sections
2. Modify emitter to detect and preserve marked sections
3. Manually write all documentation

**Pros:**
- Maximum control over doc quality and phrasing
- Docs live directly in source files

**Cons:**
- Extremely fragile -- emitter must parse and merge existing file content
- High risk of data loss during regeneration if markers are malformed
- Does not scale: ~400 members to document manually
- No automation benefit from existing JSDoc
- Marker-based preservation is error-prone with structural changes (new properties,
  renamed types, removed members)

**Maintenance burden:** Very high. Every regeneration risks losing or corrupting
hand-written docs. Every API change requires manual updates.

## Decision: Strategy A -- Enhance Emitter

**Rationale:**

1. **The infrastructure already exists.** The extractor already captures `documentation`
   at every level. The emitter already has `WriteDocComment()`. Strategy A is an
   incremental enhancement to existing, working code.

2. **Upstream JSDoc quality is high.** With 1,434 JSDoc comments in `monaco.d.ts`,
   most types and properties already have meaningful documentation. The emitter just
   needs to emit it more completely.

3. **Zero maintenance burden.** When Monaco is upgraded, regeneration automatically
   picks up new or improved JSDoc. No sidecar files to update, no markers to preserve.

4. **Consistency.** All generated types get the same documentation treatment
   automatically, eliminating the risk of some types being well-documented while
   others are neglected.

5. **The gaps are acceptable.** Where upstream JSDoc is missing (some enum members,
   some utility types), the generated C# types will also lack docs. This is acceptable
   because: (a) the upstream Monaco project actively improves its JSDoc, (b) the most
   important types (editor options, completion items, markers) already have thorough
   JSDoc, and (c) hand-written files (Helpers, StandaloneEditorConstructionOptions)
   are already documented via fn-5.5.

For any C#-specific documentation needs (platform caveats, exception documentation),
these belong on the **hand-written wrapper APIs** (CodeEditor, presenters,
LanguagesHelper), not on the generated type-mirror files. The hand-written APIs are
already documented via fn-5.5.

## Implementation Plan for fn-5.8

### Phase 1: Emitter XML Doc Enhancement

Modify `CSharpEmitter.WriteDocComment()` and related methods:

1. **`<summary>` tags** -- Already implemented. Verify all type-level, property-level,
   method-level, and enum-member-level documentation flows through.

2. **`<param>` tags** -- For methods, constructors, and call signatures, emit `<param>`
   tags from `ParameterInfo.Documentation`. The extractor's `getParameterDocumentation()`
   already reads `@param` JSDoc tags.

3. **`<returns>` tags** -- For methods with non-void return types, emit a `<returns>`
   tag if the JSDoc contains return type documentation. Check for `@returns` tags in
   the extractor (may need a minor extractor enhancement to capture `@returns`
   separately from the main comment body).

4. **`<remarks>` with TypeDoc links** -- For type-level documentation, append a
   `<remarks>` block with a `<see href="..."/>` link to the corresponding Monaco
   TypeDoc page. The link pattern must be namespace-aware and symbol-kind-aware:

   | Kind | Namespace | URL pattern |
   |------|-----------|-------------|
   | interface | `monaco.editor` | `https://microsoft.github.io/monaco-editor/typedoc/interfaces/editor.{TypeName}.html` |
   | interface | `monaco.languages` | `https://microsoft.github.io/monaco-editor/typedoc/interfaces/languages.{TypeName}.html` |
   | interface | `monaco` | `https://microsoft.github.io/monaco-editor/typedoc/interfaces/{TypeName}.html` |
   | enum | `monaco.editor` | `https://microsoft.github.io/monaco-editor/typedoc/enums/editor.{TypeName}.html` |
   | enum | `monaco.languages` | `https://microsoft.github.io/monaco-editor/typedoc/enums/languages.{TypeName}.html` |
   | enum | `monaco` | `https://microsoft.github.io/monaco-editor/typedoc/enums/{TypeName}.html` |
   | class | `monaco` | `https://microsoft.github.io/monaco-editor/typedoc/classes/{TypeName}.html` |
   | type alias | (any) | `https://microsoft.github.io/monaco-editor/typedoc/types/{ns}.{TypeName}.html` |

   The emitter should construct links based on the source namespace and symbol kind
   from the intermediate model. If a specific URL pattern cannot be determined (e.g.,
   for unusual symbol types), fall back to the TypeDoc index page:
   `https://microsoft.github.io/monaco-editor/typedoc/index.html`

5. **Enum member documentation** -- The `WriteDocComment()` call site for enum members
   is currently missing. Add calls to `WriteDocComment(sb, member.Documentation, indent)`
   for each enum member in `EmitEnum()` and `EmitTypeAliasEnum()`.

### Phase 2: Regenerate and Validate

1. Run the extraction pipeline:
   ```bash
   npx tsx tools/monaco-type-extractor/src/index.ts -- \
     node_modules/monaco-editor/monaco.d.ts \
     -o tools/monaco-type-extractor/output/model.json
   ```

2. Run the emitter:
   ```bash
   dotnet run --project tools/MonacoTypeEmitter -- \
     --input tools/monaco-type-extractor/output/model.json \
     --output MonacoEditorComponent/Monaco/
   ```

3. Build and verify:
   ```bash
   dotnet build MonacoEditorComponent.slnx --no-restore
   ```

4. Update `MonacoEditorComponent/Monaco/.editorconfig` to re-enable CS1591:
   Change `dotnet_diagnostic.CS1591.severity = none` to `warning` for `[*.cs]` in
   that directory, verifying that generated files now have sufficient coverage.

5. Run full warnaserror check:
   ```bash
   dotnet build MonacoEditorComponent.slnx /warnaserror:CS1591
   ```

### Phase 3: Update Snapshot Tests

The emitter has snapshot tests in `tools/MonacoTypeEmitter.Tests/`. After changing
the emitter's doc output, update the snapshot baselines to reflect the new XML doc
format.

### Phase 4: Verify Hand-Written File Exclusion

Confirm that hand-written files in `Monaco/` (Helpers, StandaloneEditorConstructionOptions,
LanguagesHelper) are:
- On the emitter's ignore list (not overwritten during regeneration)
- Already documented via fn-5.5
- Not affected by the emitter changes

## Acceptance Criteria for fn-5.8

Derived from the chosen strategy:

- [ ] Emitter generates `<summary>` tags for all types, properties, methods, and
      enum members that have upstream JSDoc
- [ ] Emitter generates `<param>` tags for method/constructor parameters that have
      `@param` JSDoc
- [ ] Emitter generates `<returns>` tags for methods/functions that have `@returns`
      JSDoc (requires minor extractor enhancement to capture `@returns` separately)
- [ ] Emitter generates `<remarks>` with Monaco TypeDoc cross-reference links for
      type-level documentation, using namespace-aware and symbol-kind-aware URL patterns
- [ ] All generated files regenerated with the enhanced emitter
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds with 0 errors
- [ ] `MonacoEditorComponent/Monaco/.editorconfig` updated to re-enable CS1591 for
      generated types
- [ ] `dotnet build MonacoEditorComponent.slnx /warnaserror:CS1591` passes (generated
      files have sufficient XML doc coverage)
- [ ] Snapshot tests updated for new XML doc format (including `<param>`, `<returns>`,
      `<remarks>` tag output)
- [ ] No regressions in existing hand-written documentation

## References

- [Monaco Editor TypeDoc API](https://microsoft.github.io/monaco-editor/typedoc/index.html)
- [Microsoft Learn - XML Documentation Tags](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags)
- [monaco.d.ts](https://github.com/microsoft/monaco-editor/blob/main/release/esm/vs/editor/editor.api.d.ts) (upstream TypeScript declarations parsed by the extractor)
- Emitter source: `tools/MonacoTypeEmitter/Emitter/CSharpEmitter.cs`
- Extractor source: `tools/monaco-type-extractor/src/extractor.ts`
- Intermediate model: `tools/monaco-type-extractor/src/model.ts`
