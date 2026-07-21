# fn-14-uno-hybridwebview-control-library.2 Core interfaces, control, and XAML template

## Description
Define the core interfaces, HybridWebView control class, dependency properties, event args, and XAML template. This establishes the public API surface that both platform presenters implement.

**Size:** M
**Files:** `HybridWebViewComponent/HybridWebView/IHybridWebViewPresenter.cs`, `HybridWebViewComponent/HybridWebView/HybridWebView.cs`, `HybridWebViewComponent/HybridWebView/HybridWebViewRawMessageReceivedEventArgs.cs`, `HybridWebViewComponent/HybridWebView/HybridWebViewDotNetMethodRegistration.cs`, `HybridWebViewComponent/Themes/Generic.xaml`

## Approach

- Follow `ICodeEditorPresenter.cs` pattern for `IHybridWebViewPresenter`:
  - `Task InitializeAsync(FrameworkElement host)`
  - `Task<string?> EvaluateJavaScriptAsync(string script)`
  - `Task<TResult?> InvokeJavaScriptAsync<TResult>(string methodName, params object?[] args)`
  - `Task SendRawMessageAsync(string message)`
  - `event EventHandler<HybridWebViewRawMessageReceivedEventArgs> RawMessageReceived`
  - `void RegisterDotNetMethod(string name, Func<JsonElement[], Task<JsonElement?>> handler)` — AOT-safe method registration (replaces MAUI's reflection-based `SetInvokeJavaScriptTarget`)
  - `void NavigateToLocalContent(string hybridRoot, string defaultFile)`
  - `Task DisposeAsync()`
- `HybridWebView` control class:
  - DependencyProperties: `DefaultFile` (string, default "index.html"), `HybridRoot` (string, default "wwwroot")
  - Runtime presenter factory in `OnApplyTemplate()` using `OperatingSystem.IsBrowser()` — follow `CodeEditor.cs` pattern
  - `RawMessageReceived` event, `SendRawMessage()`, `EvaluateJavaScriptAsync()`, `InvokeJavaScriptAsync<T>()`, `RegisterDotNetMethod()`
- Adapt MAUI's `HybridWebView.cs` API surface, translating Handler calls to Presenter calls
- `Generic.xaml` with `ContentPresenter` template (follow `MonacoEditorComponent/Themes/Generic.xaml`)
- Event args: `HybridWebViewRawMessageReceivedEventArgs` with `Message` property (adapted from MAUI)

## Key context

- MAUI's `SetInvokeJavaScriptTarget<T>` uses `[DynamicallyAccessedMembers]` reflection — we replace with explicit `RegisterDotNetMethod()` for AOT safety
- The presenter interface must support both WebView2 (Desktop) and iframe/BrowserHtmlElement (WASM) without platform-specific types leaking into the interface
- `JsonElement` parameters in `RegisterDotNetMethod` keep the interface trimming-safe while allowing flexible dispatch
## Acceptance
- [ ] `IHybridWebViewPresenter` interface defined with all interop methods
- [ ] `HybridWebView` control compiles with DependencyProperties (`DefaultFile`, `HybridRoot`)
- [ ] `OnApplyTemplate()` creates correct presenter based on `OperatingSystem.IsBrowser()`
- [ ] `RegisterDotNetMethod()` API is AOT-safe (uses `Func<JsonElement[], Task<JsonElement?>>`, no reflection)
- [ ] `HybridWebViewRawMessageReceivedEventArgs` defined
- [ ] `Generic.xaml` with control template present
- [ ] Public API surface matches MAUI's HybridWebView where applicable (DefaultFile, HybridRoot, SendRawMessage, EvaluateJavaScriptAsync, InvokeJavaScriptAsync)
- [ ] Project builds successfully
## Done summary
- Task completed
## Evidence
- Commits:
- Tests:
- PRs: