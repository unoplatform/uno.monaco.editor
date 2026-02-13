# fn-13-fix-desktop-webview2-runtime-bugs.1 Fix presenter lifecycle — stop recreating per-instance presenters

## Description
Fix the root cause of desktop flickering, focus loss, and Editor_Unloaded cycling: `OnApplyTemplate()` in `CodeEditor.cs:358-432` unconditionally creates a new `DesktopCodeEditorPresenter` (and thus a new WebView2 + JSON-RPC bridge) every time it is called, even when the CodeEditor instance already has a healthy presenter. Tab switches trigger re-templating, which destroys the existing presenter and bootstraps from scratch — causing 3x init cycles, black-then-white flash, and focus ping-pong.

**Instance model:** Each CodeEditor instance owns its own DesktopCodeEditorPresenter. Multiple editors on the same page or different tabs are fully independent — there is NO presenter sharing across instances. The bug is that a single CodeEditor destroys and recreates *its own* presenter every time it re-enters the visual tree. The debug log shows 3 different presenter IDs (0366FF35, 02DFBF24, 00AFB5B5) for the same editor.

**Critical additional fix:** The 100ms deferred teardown CTS creates a race condition. If the Unloaded→Loaded cycle takes >100ms, `DeferredTeardownAsync` executes hard teardown while the control is being re-initialized, leaving the presenter in a half-initialized state.

**Size:** M
**Files:**
- `MonacoEditorComponent/CodeEditor/CodeEditor.cs` (OnApplyTemplate guard, deferred teardown race fix, lifecycle state)
- `MonacoEditorComponent/CodeEditor/CodeEditor.Events.cs` (integration with lifecycle changes, potential BeginInit/EndInit)
- `MonacoEditorComponent/CodeEditor/CodeEditor.Properties.cs` (if BeginInit/EndInit is implemented, update DP handler guards)
- `MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs` (disposal, identity tracking)
- `MonacoEditorComponent/Themes/Generic.xaml` (if template structure needs adjustment)

## Approach

### Primary fix: stop recreating presenters
- Each CodeEditor must retain its own DesktopCodeEditorPresenter across unload/load cycles. `OnApplyTemplate()` should detect that this editor already has a healthy presenter (check `_view is DesktopCodeEditorPresenter` and `IsCoreWebView2Initialized` at line 75) and skip the destroy/recreate path.
- Investigate whether `OnApplyTemplate` can be a no-op for the presenter path — if `viewHost.Content` already references the correct presenter, nothing needs to change. The template's ContentPresenter may or may not preserve its Content reference across unload/load; determine this empirically.
- If re-assignment is needed: `viewHost.Content = _view` (same instance, no visual tree change, no new WebView2).
- Ensure the only time a new presenter is created is on first init (no existing presenter) or if the existing one is genuinely disposed/unhealthy.

### Deferred teardown race fix
- Hard teardown (`DeferredTeardownAsync`) must verify the control is still unloaded before nulling state: `if (!IsLoaded) { ... }`
- Soft-reload detection in `CodeEditor_Loaded` should check `_lifecycleState != EditorLifecycleState.Unloaded` rather than relying solely on `_unloadCts != null`
- If hard teardown already ran, `OnApplyTemplate` must detect this and re-invoke `InitialiseWebObjects` bridge setup to restore torn-down bridge targets on the existing presenter

### BeginInit/EndInit investigation (secondary)
Investigate whether a deferral mechanism for DP change handlers (inspired by `ISupportInitialize` in WinForms/WPF) would help avoid unnecessary JS roundtrips during initialization. This is defense-in-depth, not the root cause fix:
- Currently each DP handler independently checks `IsEditorLoaded` before pushing to Monaco
- A BeginInit/EndInit pattern would suppress JS pushes during init, then batch-apply via `ApplyInitialPropertyValues()` when init completes
- This may or may not be needed once the primary lifecycle fix prevents re-creation. Determine empirically.
- If implemented: only JS-push side-effects are deferred; DP value storage, bindings, and `NotifyPropertyChanged` continue normally.

### Pitfall guidance
- `.flow/memory/pitfalls.md` line 44: "child element event handlers must only be detached when the child is replaced (OnApplyTemplate), not on Unloaded"
- Line 47: "add IsLoaded guards in handlers to prevent late callbacks from re-initializing"
- Line 137: "deferred teardown CTS field must be cleared after hard teardown executes"
- Practice-scout finding: `DefaultBackgroundColor = Colors.Transparent` (or theme-matching color) set before first render prevents white flash. `MoveFocusRequested` handler can block focus steal during init.

## Key context

- The `ContentPresenter` named "View" in `Generic.xaml:17-20` hosts the presenter. `OnApplyTemplate` gets this presenter and sets `viewHost.Content = presenter`.
- WinUI `TabView` moves controls in/out of visual tree on tab switch, firing Unloaded/OnApplyTemplate/Loaded. This is expected behavior, not a bug in WinUI.
- The `_lifecycleState` enum (`EditorLifecycleState`) tracks Unloaded → Loading → Loaded. The guard must check this state to avoid reusing a presenter that failed initialization.
- `_testHarnessInitialized` in `EditorControl.xaml.cs:363` prevents duplicate test setup — verify this still works after the fix.
- Current `ApplyInitialPropertyValues()` at `CodeEditor.Events.cs:419-457` already implements the correct push order: language → options → theme → content → decorations → markers.
- Multiple editor instances must work independently — each `CodeEditor` instance has its own init state, presenter, and lifecycle.

## Acceptance
- [ ] Each CodeEditor retains its own presenter across unload/load cycles — no destroy/recreate on tab switch
- [ ] `OnApplyTemplate()` is a no-op (or minimal re-assignment) for the presenter path when the existing presenter is healthy
- [ ] Tab switching is instantaneous — no visible delay, flash, or flicker
- [ ] Multiple editor instances on the same page or different tabs work correctly and independently
- [ ] Debug log shows single `DesktopCodeEditorPresenter()` constructor call per editor lifetime (not per tab switch)
- [ ] `Editor_Unloaded` fires at most once per actual editor close (not on tab switch)
- [ ] Deferred teardown race resolved: hard teardown checks `IsLoaded` before nulling state
- [ ] Soft-reload detection uses `_lifecycleState` check, not just `_unloadCts != null`
- [ ] If hard teardown already ran, presenter bridge setup is restored
- [ ] WebView2 is properly disposed when the editor control is actually removed from the tree (tab closed, page navigated away)
- [ ] BeginInit/EndInit investigation documented: whether it helps, decision on whether to implement
- [ ] Existing desktop CDP tests pass (no regression)
- [ ] Solution builds clean for both net10.0-desktop and net10.0-browserwasm targets
## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
