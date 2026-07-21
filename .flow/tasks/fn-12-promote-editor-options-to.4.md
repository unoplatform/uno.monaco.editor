# fn-12-promote-editor-options-to.4 Update test app and documentation for new DependencyProperties

## Description
Update the test app to exercise the new DependencyProperties and update project documentation.

**Size:** M
**Files:**
- `MonacoEditorTestApp/MainPage.xaml` — add controls bound to new DPs
- `MonacoEditorTestApp/MainPage.xaml.cs` — add ViewModel properties if needed
- `docs/cookbook.md` — add DP usage examples
- `docs/getting-started.md` — update quick-start with common DP usage
- `README.md` — update feature list / API surface description
- `CHANGELOG.md` — add entry for new DPs

## Approach

### Test app updates

- Add a settings panel (or extend existing) with controls bound to the new DPs:
  - Theme: ComboBox with vs, vs-dark, hc-black, hc-light
  - FontSize: NumberBox or Slider
  - FontFamily: TextBox or ComboBox with common coding fonts
  - WordWrap: ComboBox (off, on, wordWrapColumn, bounded)
  - LineNumbers: ComboBox (off, on, relative, interval)
  - IsMinimapEnabled: ToggleSwitch
  - TabSize: NumberBox
  - IsFoldingEnabled: ToggleSwitch
  - RenderWhitespace: ComboBox (none, boundary, selection, trailing, all)
- Use x:Bind with Mode=TwoWay to demonstrate bidirectional binding
- Verify settings panel works on both WASM and Desktop targets

### Documentation updates

- Add a "DependencyProperties" section to cookbook.md showing common patterns:
  - Setting in XAML
  - Style Setters
  - x:Bind
  - TemplateBinding (in a custom wrapper control example)
- Update getting-started.md with the most common DPs (Theme, FontSize, Language)
- Update README feature list to mention bindable DPs
- Add CHANGELOG entry

## Key context

- docs-gap-scout found: README.md, cookbook.md, getting-started.md, CHANGELOG.md all need updates
- Test app currently uses code-behind to set Options — show the DP-based approach as the primary API
## Acceptance
- [ ] Test app has a settings panel (or section) with controls for at least 8 new DPs
- [ ] Settings use x:Bind with TwoWay binding
- [ ] Settings panel works on both WASM and Desktop targets
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm` succeeds
- [ ] `dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop` succeeds
- [ ] cookbook.md has a "DependencyProperties" section with XAML, Style Setter, and x:Bind examples
- [ ] getting-started.md updated with common DP usage
- [ ] README.md feature list mentions bindable DPs
- [ ] CHANGELOG.md has an entry for the new DPs
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
