# fn-10-fix-reverted-emitter-docs-enable-cdp-ci.1 Fix emitter edge cases for exotic TS identifiers

## Description
Fix CSharpEmitter to handle exotic TypeScript identifiers that produced invalid C# during the fn-5.8 regeneration, causing the revert in commit `6e7fee0`.

**Size:** M
**Files:** `tools/MonacoTypeEmitter/Emitter/CSharpEmitter.cs`, `tools/MonacoTypeEmitter/Emitter/NameMapper.cs`, `tools/MonacoTypeEmitter.Tests/`

## Approach
- The intermediate model (`model.json`) contains 4 `$`-prefixed properties: `$comment`, `$id`, `$ref`, `$schema` (JSON Schema properties in `IJSONSchema`)
- The revert commit cited: `$comment`/`$id` (invalid C# identifiers), `'semanticHighlighting.enabled'` (dotted identifier in quotes), `obj is IPosition IsIPosition` (TypeScript type guard return syntax)
- Fix `NameMapper` or `CSharpEmitter` to sanitize these: strip `$` prefix or replace with valid C# identifier (e.g., `Dollar` prefix or `JsonPropertyName` attribute mapping)
- For dotted identifiers, use `JsonPropertyName` attribute to preserve wire name while using a C#-valid property name
- For type guard returns (`x is Type`), emit `bool` return type with doc comment noting the TS semantics
- Add snapshot tests for each edge case to prevent regression
- Verify existing 19 snapshot tests still pass

## Key context
- The revert commit `6e7fee0` shows the exact invalid patterns; `git show 6e7fee0 --stat` shows 570 files changed
- The `model.json` has only 4 `$`-prefixed properties, all in `IJSONSchema` interface
- No dotted identifiers or type guard returns found in `model.json` — these may come from the emitter's handling of TS `extends`/implements patterns
- Existing ignore list at `tools/MonacoTypeEmitter/Emitter/IgnoreList.cs` already skips some problematic types
## Acceptance
- [ ] `$comment`, `$id`, `$ref`, `$schema` properties emit valid C# with `[JsonPropertyName("$...")]` attributes
- [ ] Dotted TypeScript identifiers produce valid C# property names
- [ ] TypeScript type guard returns (`obj is IPosition`) emit as `bool` return type
- [ ] New snapshot tests cover each exotic identifier edge case
- [ ] All 19+ existing snapshot tests still pass
- [ ] `dotnet test --project tools/MonacoTypeEmitter.Tests/` passes
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
