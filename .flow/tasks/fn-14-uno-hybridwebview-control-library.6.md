# fn-14-uno-hybridwebview-control-library.6 Sample page and end-to-end validation

## Description
Add a sample HybridWebView page to `MonacoEditorTestApp` with test web content that validates all interop paths on both Desktop and WASM.

**Size:** M
**Files:** `MonacoEditorTestApp/Views/HybridWebViewPage.xaml`, `MonacoEditorTestApp/Views/HybridWebViewPage.xaml.cs`, `MonacoEditorTestApp/wwwroot/index.html`, `MonacoEditorTestApp/wwwroot/app.js`, `MonacoEditorTestApp/MonacoEditorTestApp.csproj` (project reference + content items)

## Approach

- Add `HybridWebViewComponent` project reference to `MonacoEditorTestApp.csproj`
- Create sample web content in `MonacoEditorTestApp/wwwroot/`:
  - `index.html` — test page with buttons for each interop path
  - `app.js` — JS functions for C#→JS testing, calls to `window.HybridWebView.InvokeDotNet()` for JS→C# testing
  - Include `HybridWebView.js` bridge script via `<script>` tag
- Create `HybridWebViewPage.xaml` with:
  - `<hwv:HybridWebView HybridRoot="wwwroot" DefaultFile="index.html" />`
  - Buttons for: "Eval JS", "Invoke JS Method", "Send Raw Message"
  - TextBlock showing received messages/results
- Code-behind registers .NET methods via `RegisterDotNetMethod()` and wires events
- Add navigation entry to test app's main page (follow existing app navigation pattern)
- Ensure wwwroot content is included as Content items (for Desktop) and as appropriate assets (for WASM)

## Key context

- `MonacoEditorTestApp` is already multi-targeted: `net10.0-desktop;net10.0-browserwasm`
- Follow existing page pattern in MonacoEditorTestApp for XAML page structure and navigation
- Web content must be packaged differently per platform: Content items for Desktop, static web assets for WASM
- This is the primary validation vehicle — test ALL interop paths: C#→JS eval, C#→JS invoke, JS→C# invoke, raw messages both directions
## Acceptance
- [ ] `MonacoEditorTestApp` references `HybridWebViewComponent`
- [ ] Sample `wwwroot/` with `index.html` and `app.js` present
- [ ] `HybridWebViewPage.xaml` renders HybridWebView control
- [ ] Navigation entry added to test app's main page
- [ ] C#→JS: `EvaluateJavaScriptAsync("2+2")` returns "4" on both platforms
- [ ] C#→JS: `InvokeJavaScriptAsync<string>("greet", "World")` returns expected result on both platforms
- [ ] JS→C#: `InvokeDotNet("Echo", "hello")` invokes registered method on both platforms
- [ ] Raw messages: bidirectional `SendRawMessage` works on both platforms
- [ ] App builds: `dotnet build -f net10.0-browserwasm` and `dotnet build -f net10.0-desktop`
- [ ] Full solution builds: `dotnet build MonacoEditorComponent.slnx`
## Done summary
- Task completed
## Evidence
- Commits:
- Tests:
- PRs: