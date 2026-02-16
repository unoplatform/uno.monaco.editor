# fn-15-spike-composition-mode-webview2-to.2 Validate: flickering scenarios, airspace, and document findings

## Description

Run the 7 test scenarios in both Mode A (HWND) and Mode C (DComp + ANGLE), side by side. Document which scenarios flicker in each mode, with observations. The critical new test is scenario 6 (Skia over WebView) which validates airspace fix.

**Size:** S
**Files:** spike findings document (in the spike project or `.flow/` memory)

## Approach

Run each scenario in both modes. For each, note: visible flicker (Y/N), white flash (Y/N), airspace correct (Y/N), behavior description.

**Scenarios:**

1. **Show/Hide toggle**: Press [H] rapidly 10x. Count white flashes per mode.
2. **Dark theme load**: Set `DefaultBackgroundColor` before navigation. Observe initial load.
3. **Resize**: Drag window edge while WebView is visible. Observe during resize.
4. **Destroy/recreate**: Press [R] to destroy WebView and recreate (simulates Uno's `OnApplyTemplate` re-templating). Observe the gap.
5. **Two WebViews**: Press [T] to create two WebViews. Toggle visibility of each independently. Check z-order.
6. **Skia over WebView** (airspace test): Observe the Skia-rendered rectangle that overlaps WebView2. In Mode A it should be hidden behind WebView2 (airspace problem). In Mode C it should render on top (airspace solved).
7. **Opacity animation**: Press [O] to animate opacity 0→1 over 500ms. Observe smoothness.

**Document findings:**
- Results table: scenario x mode → observation
- Screenshots or screen recordings if possible
- Clear verdict: does full DComp + ANGLE fix the flickering AND airspace?
- Any new issues introduced by Mode C (input gaps, cursor problems, performance)
- Recommendation: proceed to Uno integration (upstream PR to add DComp renderer), or abandon

## Key context

- Mode A should reproduce at least scenarios 1 (show/hide) and 4 (destroy/recreate) flickering — these are the exact problems reported in fn-13
- Mode C should fix all flickering scenarios AND scenario 6 (airspace) — this is the key differentiator from a simple DComp overlay
- Input forwarding quality in Mode C is itself a finding — if mouse is unreliable, that's a significant cost
- The findings document should be actionable: what specific files in `unoplatform/uno` would need changing and what's the estimated scope

## Acceptance
- [ ] All 7 scenarios tested in Mode A (HWND) — flickering reproduced in at least 2
- [ ] All 7 scenarios tested in Mode C (DComp + ANGLE) — results documented
- [ ] Scenario 6 (airspace) specifically validated: Skia element renders on top in Mode C
- [ ] Side-by-side comparison table with clear observations per scenario per mode
- [ ] Verdict documented: does full DComp + ANGLE eliminate flickering AND airspace?
- [ ] New issues (if any) documented: input gaps, cursor, performance problems
- [ ] Recommendation: next steps (Uno upstream PR scope, or abandon)

## Done summary
TBD

## Evidence
- Commits:
- Tests:
- PRs:
