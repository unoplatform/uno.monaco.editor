# fn-10-fix-reverted-emitter-docs-enable-cdp-ci.1 Fix emitter edge cases for exotic TS identifiers

## Description
Fix CSharpEmitter and TypeMapper to handle exotic TypeScript identifiers that produced invalid C# during the fn-5.8 regeneration, causing the revert in commit `6e7fee0`.

**Size:** M
**Files:** `tools/MonacoTypeEmitter/Emitter/CSharpEmitter.cs`, `tools/MonacoTypeEmitter/Emitter/NameMapper.cs`, `tools/MonacoTypeEmitter/Emitter/TypeMapper.cs`, `tools/MonacoTypeEmitter.Tests/`

## Approach
- The intermediate model (`model.json`) contains 4 `$`-prefixed properties: `$comment`, `$id`, `$ref`, `$schema` (JSON Schema properties in `IJSONSchema`)
- The revert commit cited: `$comment`/`$id` (invalid C# identifiers), `'semanticHighlighting.enabled'` (dotted identifier in quotes), `obj is IPosition IsIPosition` (TypeScript type guard return syntax)
- Fix `NameMapper` or `CSharpEmitter` to sanitize these: strip `$` prefix or replace with valid C# identifier (e.g., `Dollar` prefix or `JsonPropertyName` attribute mapping)
- For dotted identifiers, use `JsonPropertyName` attribute to preserve wire name while using a C#-valid property name
- **For type guard returns (`x is Type`)**: update `TypeMapper.cs` to detect TS type predicate patterns (currently passed through as raw `intrinsic` text from extractor) and map them deterministically to `bool` return type, with XML doc comment noting the TS semantics. If the extractor produces insufficient data, also update `extractor.ts` to emit a recognizable representation.
- Add snapshot tests for each edge case to prevent regression
- Verify existing 19 snapshot tests still pass

## Key context
- The revert commit `6e7fee0` shows the exact invalid patterns; `git show 6e7fee0 --stat` shows 570 files changed
- The `model.json` has only 4 `$`-prefixed properties, all in `IJSONSchema` interface
- No dotted identifiers or type guard returns found in `model.json` — these may come from the emitter's handling of TS `extends`/implements patterns
- Existing ignore list at `tools/MonacoTypeEmitter/Emitter/IgnoreList.cs` already skips some problematic types
- `TypeMapper.cs` currently has no dedicated handling for TS type predicates; they arrive as raw text and get emitted as-is, producing invalid C# return types
## Acceptance
- [ ] `$comment`, `$id`, `$ref`, `$schema` properties emit valid C# with `[JsonPropertyName("$...")]` attributes
- [ ] Dotted TypeScript identifiers produce valid C# property names
- [ ] TypeScript type guard returns (`obj is IPosition`) emit as `bool` return type via `TypeMapper.cs` mapping (not just string replacement)
- [ ] Snapshot test proves type predicate → `bool` mapping specifically
- [ ] New snapshot tests cover each exotic identifier edge case
- [ ] All 19+ existing snapshot tests still pass
- [ ] `dotnet test --project tools/MonacoTypeEmitter.Tests/` passes
## Done summary
Fixed emitter edge cases for exotic TS identifiers: $-prefixed properties sanitized via NameMapper with [JsonPropertyName] wire-name preservation, dotted/quoted identifiers joined as PascalCase, and TypeScript type predicates mapped to bool in TypeMapper. Added 4 snapshot tests covering all edge cases; all 23 emitter tests pass.
## Evidence
- Commits: 5f658177edfef482260a8ff66fe8782f2f071b38
- Tests: dotnet test --project tools/MonacoTypeEmitter.Tests/
- PRs: