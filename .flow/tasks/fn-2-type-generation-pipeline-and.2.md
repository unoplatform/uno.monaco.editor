# fn-2-type-generation-pipeline-and.2 Migrate string-enum types to JsonStringEnumMemberName

## Description
Replace all custom Newtonsoft string-enum converter classes with `[JsonStringEnumMemberName]` attributes on enum members and per-enum `[JsonConverter(typeof(JsonStringEnumConverter<T>))]`. Delete the converter classes. Numeric enums are left untouched.

**Size:** M
**Files:** ~30 enum/converter files in MonacoEditorComponent/Monaco/Editor/ and Monaco/Helpers/

## Approach

**Step 1: Inventory all enums from code** — Scan `MonacoEditorComponent/Monaco/` for all files containing `class.*Converter.*JsonConverter` to find all converter classes, then verify against enum declarations. Classify each as string-backed or numeric.

Preliminary string-backed enums (to be verified from code):
`AcceptSuggestionOnEnter`, `AccessibilitySupport`, `AutoClosingBrackets`, `AutoClosingOvertype`, `AutoClosingQuotes`, `AutoFindInSelection`, `AutoIndent`, `AutoSurround`, `CursorBlinking`, `CursorStyle`, `CursorSurroundingLinesStyle`, `FoldingStrategy`, `InsertMode`, `LineNumbersType`, `MatchBrackets`, `MouseStyle`, `MultiCursorModifier`, `MultiCursorPaste`, `Multiple`, `RenderLineHighlight`, `RenderWhitespace`, `ScrollbarBehavior`, `Show`, `Side`, `SnippetSuggestions`, `SuggestSelection`, `TabCompletion`, `WordWrap`, `WrappingIndent`, `TextDecoration`

Preliminary numeric enums (to be verified from code):
`MarkerSeverity`, `CompletionItemKind`, `CompletionItemInsertTextRule`, `SuggestTriggerKind`, `TrackedRangeStickiness`, `EndOfLinePreference`, `EndOfLineSequence`

**Important:** Rebuild this inventory from actual code at implementation time. The above lists are preliminary and must be verified.

**Step 2: For each string-backed enum:**
1. Add `[JsonStringEnumMemberName("wireValue")]` to each enum member
2. Add `[JsonConverter(typeof(JsonStringEnumConverter<EnumType>))]` on the enum type (required since there is no global `UseStringEnumConverter`)
3. Delete the custom converter class
4. Update `using` directives: remove `Newtonsoft.Json`, add `System.Text.Json.Serialization`

- Special case: `TextDecoration` has hyphenated values like `"line-through"` — `[JsonStringEnumMemberName]` handles this correctly

**Step 3: Verify** — Zero custom enum converter classes remain.

**Note:** `ContextKey` at `MonacoEditorComponent/Monaco/Editor/ContextKey.cs` is NOT a string-enum converter — it is a model type belonging to task 3 scope.

## Key context

- `[JsonStringEnumMemberName]` is .NET 9+ — the project targets net10.0 (per fn-1 TFM consolidation), so this is fully supported
- Do NOT use global `UseStringEnumConverter` on the context — that would break numeric enums
- Each string-backed enum needs its own `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` attribute
- Add contract tests for at least 3 representative string enums (one simple like CursorBlinking, one with hyphens like TextDecoration, one with multiple values like AutoIndent) verifying correct wire format
- Validate golden baselines (from task 1) still match after migration

## Acceptance
- [ ] Complete inventory of all enums rebuilt from actual code (string-backed vs numeric)
- [ ] All string-backed enum types have `[JsonStringEnumMemberName]` on each member
- [ ] All string-backed enum types have `[JsonConverter(typeof(JsonStringEnumConverter<T>))]`
- [ ] All custom enum converter classes deleted (zero remaining)
- [ ] Numeric enums untouched — still serialize as integers
- [ ] `using Newtonsoft.Json` removed from all modified enum files
- [ ] Contract tests verify string enum round-trip (at least 3 representative enums)
- [ ] Golden baseline tests still pass
- [ ] Build succeeds

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
