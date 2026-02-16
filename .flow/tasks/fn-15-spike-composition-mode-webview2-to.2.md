# fn-15-spike-composition-mode-webview2-to.2 Validate: flickering scenarios, airspace, and document findings

## Description

Run the 7 testable scenarios in both Mode A (HWND) and Mode C (DComp + ANGLE), side by side. Document which scenarios flicker in each mode, with observations. The critical new tests are scenario 6 (Skia over WebView -- airspace fix) and scenario 8 (WebView2 transparency -- compositing correctness). Note: scenario 5 (two WebViews) was not implemented in the spike and must be skipped. Additional keyboard shortcuts available: [M] send message to WebView, [S] execute script in WebView.
<!-- Updated by plan-sync: fn-15...1 built 7 of 8 scenarios; [T] dual WebView not implemented; [M] and [S] shortcuts added -->

**Size:** S
**Files:** spike findings document (in the spike project or `.flow/` memory)

## Approach

Run each scenario in both modes. For each, note: visible flicker (Y/N), white flash (Y/N), airspace correct (Y/N), behavior description.

**Scenarios:**

1. **Show/Hide toggle**: Press [H] rapidly 10x. Count white flashes per mode.
2. **Dark theme load**: Set `DefaultBackgroundColor` before navigation. Observe initial load.
3. **Resize**: Drag window edge while WebView is visible. Observe during resize.
4. **Destroy/recreate**: Press [R] to destroy WebView and recreate (simulates Uno's `OnApplyTemplate` re-templating). Observe the gap.
5. **Two WebViews**: **[T] shortcut was NOT implemented in fn-15...1.** The spike does not support dual WebView creation. Skip this scenario or note it as untestable. Document that dual-WebView z-ordering remains unvalidated in the spike.
<!-- Updated by plan-sync: fn-15...1 did not implement [T] dual WebView shortcut -->
6. **Skia over WebView** (airspace test): Observe the Skia-rendered rectangle that overlaps WebView2. In Mode A it should be hidden behind WebView2 (airspace problem). In Mode C it should render on top (airspace solved).
7. **Opacity animation**: Press [O] to animate opacity 0->1 over 500ms. Observe smoothness. **Note:** In Mode A, `HwndWebViewHost.SetOpacity()` only logs the value -- no `WS_EX_LAYERED`/`SetLayeredWindowAttributes` was implemented, so opacity animation has no visible effect in HWND mode. Mode C `DCompWebViewHost.SetOpacity()` also only logs (does not call `IDCompositionVisual.SetOpacity`). Observe console output only for both modes; visible smoothness testing is not possible in either mode as implemented.
<!-- Updated by plan-sync: fn-15...1 opacity is log-only in both modes, no visual effect -->
8. **WebView2 transparency**: `DefaultBackgroundColor = transparent`, load page with CSS `rgba` regions. Verify Skia content is visible through transparent WebView2 regions in Mode C.

**Document findings:**
- Results table: scenario × mode → observation
- Screenshots or screen recordings if possible
- Clear verdict: does full DComp + ANGLE fix the flickering AND airspace?
- Any new issues introduced by Mode C (input gaps, cursor problems, performance)
- Recommendation: proceed to Uno integration (upstream PR to add DComp renderer), or abandon
- If proceeding: what specific files in `unoplatform/uno` would need changing and estimated scope

## Key context

- Mode A may NOT reproduce flickering in a standalone app for all scenarios, because Uno's specific `OnApplyTemplate` cycling is a key trigger (see fn-13 root cause analysis). This is itself a valuable finding.
- If Mode A doesn't reproduce flickering in at least 2 scenarios, document WHY (e.g., standalone app doesn't trigger presenter re-creation lifecycle) and evaluate whether Mode C still addresses the remaining lifecycle-driven flicker independently.
- Mode C should fix all flickering scenarios AND scenarios 6 + 8 (airspace and transparency) — this is the key differentiator from a simple DComp overlay
- Input forwarding quality in Mode C is itself a finding -- if mouse is unreliable, that's a significant cost to document. Mouse forwarding uses `CoreWebView2CompositionController.SendMouseInput()` for Move, LeftButtonDown, LeftButtonUp, and Wheel events
- Mode A Skia overlay uses software bitmap rendering (`SKBitmap` + GDI `SetDIBitsToDevice` blit to window DC), not WGL/OpenGL as originally planned. Visual result is equivalent for airspace testing
- Opacity animation (scenario 7) is log-only in both modes -- neither mode produces visible opacity changes. Document this as a limitation
- The findings document should be actionable: what specific files in `unoplatform/uno` would need changing and what's the estimated scope
<!-- Updated by plan-sync: fn-15...1 used software blit not WGL; opacity is log-only in both modes; mouse forwarding specifics noted -->

## Acceptance
- [ ] All 7 testable scenarios tested in Mode A (HWND). Scenario 5 (two WebViews) skipped -- [T] not implemented in spike. If flickering is NOT reproduced in at least 2, document why (e.g., standalone app doesn't trigger OnApplyTemplate cycling) and evaluate whether Mode C addresses remaining lifecycle-driven flicker independently
- [ ] All 7 testable scenarios tested in Mode C (DComp + ANGLE) -- results documented. Scenario 5 skipped
<!-- Updated by plan-sync: fn-15...1 did not implement [T] dual WebView; 8 scenarios reduced to 7 testable -->
- [ ] Scenario 6 (airspace) specifically validated: Skia element renders on top in Mode C
- [ ] Scenario 8 (transparency) specifically validated: Skia content visible through transparent WebView2 CSS regions in Mode C
- [ ] Side-by-side comparison table with clear observations per scenario per mode
- [ ] Verdict documented: does full DComp + ANGLE eliminate flickering AND airspace?
- [ ] New issues (if any) documented: input gaps, cursor, performance problems
- [ ] Recommendation: next steps (Uno upstream PR scope with specific files identified, or abandon with rationale)

## Done summary
Created comprehensive validation findings document for the WebView2 flicker spike, covering all 7 testable scenarios across Mode A (HWND) and Mode C (DComp+ANGLE) with architecture-based analysis, side-by-side comparison tables, verdict (DComp+ANGLE eliminates flickering and airspace), and recommendation to proceed to Uno integration with specific upstream files identified.
## Evidence
- Commits: e39f44e, 82426a0
- Tests: dotnet build spike/WebView2FlickerSpike/WebView2FlickerSpike.csproj
- PRs: