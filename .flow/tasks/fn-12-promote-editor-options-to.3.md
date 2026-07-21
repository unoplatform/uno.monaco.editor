# fn-12-promote-editor-options-to.3 Promote ~15 high-value options to DependencyProperties with debouncing

## Description
Add ~15 new DependencyProperties to CodeEditor for commonly-bound editor options, with bidirectional sync to Options and debounced `updateOptions` JS calls.

**Size:** M
**Files:**
- `MonacoEditorComponent/CodeEditor/CodeEditor.Properties.cs` — new DP registrations
- `MonacoEditorComponent/CodeEditor/CodeEditor.cs` — updated `Options_PropertyChanged`, debounce infrastructure, initial sync in `CodeEditor_Loaded`

## Approach

### DP registration pattern (follow existing at `CodeEditor.Properties.cs:126-130`)

Each promoted DP:
- Uses `null` as default value for nullable types (not Monaco's default)
- PropertyChanged callback writes to `Options.PropertyName`
- CLR accessor is a simple `GetValue`/`SetValue` pair (no logic)

### Bidirectional sync

Extend `Options_PropertyChanged` at `CodeEditor.cs:166-189`:
- For each promoted property, if Options value differs from DP value, sync DP ← Options
- Consider a dictionary-driven approach instead of expanding the `switch`/`if` chain

### Debouncing

Add a `DispatcherQueueTimer` debounce (~16ms) to batch `updateOptions` JS calls:
- When any Options property changes, start/reset the timer
- On timer tick, call `InvokeScriptAsync("updateOptions", options)` once
- Special-case `Language` (needs `updateLanguage`, not `updateOptions`)

### Initial load sync (follow pattern at `CodeEditor.cs:229-242`)

In `CodeEditor_Loaded`, for each promoted DP:
- If `ReadLocalValue(XxxProperty) == DependencyProperty.UnsetValue` and `Options.Xxx` has a value → populate DP from Options
- If DP is explicitly set → Options already has the value (set via PropertyChanged callback)

### Precedence

- DP explicitly set (ReadLocalValue != UnsetValue) → DP value wins, writes to Options
- DP at default (null) → Options value untouched, Monaco JS default applies
- Both set → DP wins (last write wins, DP callback fires after Options assignment)

### Properties to add

Theme (string?), FontSize (double?), FontFamily (string?), WordWrap (WordWrap?), LineNumbers (LineNumbersType?), IsMinimapEnabled (bool? → Options.Minimap.Enabled), TabSize (int?), InsertSpaces (bool?), IsFoldingEnabled (bool? → Options.Folding), RenderWhitespace (RenderWhitespace?), ScrollBeyondLastLine (bool?), RenderLineHighlight (RenderLineHighlight?), CursorStyle (CursorStyle?), CursorBlinking (CursorBlinking?), FontLigatures (bool?)

### Special cases

- `IsMinimapEnabled` maps to `Options.Minimap.Enabled` (sub-object property) — DP callback must ensure `Options.Minimap` is instantiated before setting `.Enabled`
- `IsFoldingEnabled` maps to `Options.Folding` (bool?, not a sub-object)
- `Theme` needs special handling in `Options_PropertyChanged` similar to `Language` if theme-specific JS calls are needed

## Key context

- `SetCurrentValue` does NOT exist in UWP/WinUI/Uno — use `SetValue` via CLR property setter (practice-scout finding)
- Equality checks in `SetPropertyValue<T>` prevent infinite sync loops (existing pattern is correct)
- The `IsSettingValue` guard flag at `CodeEditor.cs` handles reentrancy for `Text` property — similar pattern may be needed if any promoted DP has JS-initiated changes
- fn-6.4 type registration bug: `DecorationsProperty` is registered as `typeof(IModelDeltaDecoration)` instead of `typeof(IObservableVector<IModelDeltaDecoration>)` — avoid repeating this pattern
## Acceptance
- [ ] 15 new DependencyProperties registered on CodeEditor
- [ ] Each DP has null default value (nullable types)
- [ ] Each DP PropertyChanged callback writes to the corresponding Options property
- [ ] `Options_PropertyChanged` syncs promoted DPs back from Options when values differ
- [ ] `updateOptions` JS calls are debounced via DispatcherQueueTimer (~16ms)
- [ ] `Language` property change still triggers `updateLanguage` (not debounced with updateOptions)
- [ ] Initial sync in `CodeEditor_Loaded` populates DPs from Options when DP is at default
- [ ] XAML binding works: `<monaco:CodeEditor Theme="vs-dark" FontSize="14" />`
- [ ] Style Setters work: `<Setter Property="FontSize" Value="14" />`
- [ ] Setting Options.FontSize programmatically syncs to the FontSize DP
- [ ] Setting FontSize DP syncs to Options.FontSize
- [ ] No infinite sync loops when changing promoted properties
- [ ] `dotnet build MonacoEditorComponent.slnx --no-restore` succeeds
- [ ] Build succeeds on both `net10.0-browserwasm` and `net10.0-desktop` targets
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
