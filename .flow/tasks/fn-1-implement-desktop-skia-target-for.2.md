## Description

Refactor the presenter architecture from a single partial class to two separate concrete types with a runtime factory. Update Generic.xaml, ICodeEditorPresenter (add script execution + inbound message contracts), WebViewExtensions, and CodeEditor.OnApplyTemplate. Extract helper type abstractions for desktop variants.

**Size:** M
**Files:** MonacoEditorComponent/CodeEditor/CodeEditorPresenter.wasm.cs (rename), MonacoEditorComponent/CodeEditor/WasmCodeEditorPresenter.cs (new name), MonacoEditorComponent/CodeEditor/DesktopCodeEditorPresenter.cs (new), MonacoEditorComponent/CodeEditor/ICodeEditorPresenter.cs, MonacoEditorComponent/Extensions/WebViewExtensions.cs, MonacoEditorComponent/CodeEditor/CodeEditor.cs, MonacoEditorComponent/Themes/Generic.xaml, MonacoEditorComponent/Helpers/ParentAccessor.cs, MonacoEditorComponent/Helpers/ThemeListener.cs, MonacoEditorComponent/Helpers/KeyboardListener.cs, MonacoEditorTestApp/MonacoEditorTestApp.csproj

## Approach

- **Rename**: `CodeEditorPresenter` class → `WasmCodeEditorPresenter`. File → `WasmCodeEditorPresenter.cs`. Guard constructor: `if (!OperatingSystem.IsBrowser()) throw new PlatformNotSupportedException()`.
- **DesktopCodeEditorPresenter**: New class extending `ContentControl`, implementing `ICodeEditorPresenter`. Wraps `WebView2`. Shell in this task — full lifecycle in Task 5. Has `EnsureCoreWebView2Async()`, `SetVirtualHostNameToFolderMapping()`, navigation to host page, `WebMessageReceived` handler that normalizes payloads into `MessageReceived` event. Exposes hook for Task 5 to attach `WebView2JsonRpcMessageHandler` + `JsonRpc` instance to the WebView2 message channel.
- **StreamJsonRpc NuGet**: Add `StreamJsonRpc` package to `MonacoEditorComponent.csproj`. This is the only new NuGet dependency for the desktop bridge. Used by the `WebView2JsonRpcMessageHandler` (Task 5) and all desktop bridge classes. **AOT note**: Task 5 will configure it with `SystemTextJsonFormatter` (not Newtonsoft) — ensure no transitive Newtonsoft dependency is required.
- **ICodeEditorPresenter evolution** (locked contracts from epic):
  - **Script execution**: Add `Task<string> InvokeScriptAsync(string script)`. Return contract: always returns **raw JSON token** string. WASM wraps `NativeMethods.InvokeJS()`. Desktop wraps `CoreWebView2.ExecuteScriptAsync()`. Add acceptance for string/object/null round-trips.
  - **Message send**: Add `void PostWebMessage(string json)`. Desktop wraps `CoreWebView2.PostWebMessageAsJson()`. WASM: throws `PlatformNotSupportedException` (not used on WASM). This decouples the StreamJsonRpc transport handler from `CoreWebView2` internals and ensures cross-platform portability (Uno's `WebView2` wraps native views on all desktop platforms).
  - **Inbound message event**: Add `event EventHandler<WebViewMessageEventArgs> MessageReceived`. `WebViewMessageEventArgs` has `string MessageJson`. Desktop presenter fires this from `WebMessageReceived`. WASM presenter never fires it (uses JSExport direct calls). Task 5 consumes this for all desktop bridge routing.
- **Helper type abstractions**: Extract interfaces (`IParentAccessor`, `IThemeListener`, `IKeyboardListener`, `IDebugLogger`) from existing concrete types. `CodeEditor.Events.cs` fields change from concrete types to interfaces. Add a static helper factory: `BridgeFactory.Create(ICodeEditorPresenter presenter, DispatcherQueue queue)` that returns `(IParentAccessor, IThemeListener, IKeyboardListener, IDebugLogger)`. In this task, factory only creates WASM variants. Task 5 adds the `else` branch for desktop variants (using JsonRpc targets). This explicit factory contract is the handoff point between Task 2 and Task 5.
- **Generic.xaml**: Replace `<monaco:CodeEditorPresenter x:Name="View" />` with `<ContentPresenter x:Name="View" />`.
- **OnApplyTemplate factory**: Create correct presenter via `OperatingSystem.IsBrowser()`, set as `Content` of ContentPresenter.
- **WebViewExtensions refactor**: `RunScriptHelperAsync<T>` calls through `ICodeEditorPresenter.InvokeScriptAsync()` instead of `NativeMethods.InvokeJS()`.
- **WebView feature enablement**: Add `WebView;` to `<UnoFeatures>` in `MonacoEditorTestApp.csproj` (required for desktop WebView2).
- **Lifecycle state machine**: Current code raises `EditorLoaded` from both `CodeEditorLoaded()` and `WebView_NavigationCompleted()`, violating exactly-once semantics. Introduce an enum `EditorLifecycleState { Unloaded, Loading, Loaded }` with a single transition method. Each lifecycle event (`EditorLoading`, `EditorLoaded`) fires from exactly one state transition, preventing duplicates. Task 8's Playwright tests assert `Loaded:1` — this fix makes that assertion viable.
- **Security hardening** (desktop presenter): `AreDefaultScriptDialogsEnabled=false`, `AreDefaultContextMenusEnabled=false`, `AreHostObjectsAllowed=false`. Block external navigation.

## Acceptance

- [ ] `WasmCodeEditorPresenter` replaces `CodeEditorPresenter` (renamed, guarded)
- [ ] `DesktopCodeEditorPresenter` exists with WebView2 shell
- [ ] `ICodeEditorPresenter.InvokeScriptAsync` defined with raw-JSON-token return contract
- [ ] `ICodeEditorPresenter.PostWebMessage` defined (desktop wraps PostWebMessageAsJson, WASM throws)
- [ ] `ICodeEditorPresenter.MessageReceived` event defined with `WebViewMessageEventArgs`
- [ ] Script execution round-trip: string/object/null values return correctly on both presenters
- [ ] Helper type abstractions extracted (interfaces or base types for ParentAccessor, ThemeListener, KeyboardListener, DebugLogger)
- [ ] Generic.xaml uses ContentPresenter placeholder
- [ ] OnApplyTemplate creates correct presenter via `OperatingSystem.IsBrowser()`
- [ ] WebViewExtensions uses presenter's InvokeScriptAsync
- [ ] MonacoEditorTestApp has `WebView;` in UnoFeatures
- [ ] Helper interfaces extracted (`IParentAccessor`, `IThemeListener`, `IKeyboardListener`, `IDebugLogger`)
- [ ] `BridgeFactory` creates correct variants based on `OperatingSystem.IsBrowser()` (WASM-only in this task)
- [ ] Lifecycle state machine: `EditorLoading`/`EditorLoaded` fire exactly once (no duplicate raises)
- [ ] `StreamJsonRpc` NuGet added to `MonacoEditorComponent.csproj`
- [ ] DesktopCodeEditorPresenter exposes hook for JsonRpc attachment (consumed by Task 5)
- [ ] WASM still works end-to-end (no regression)
- [ ] Desktop presenter security settings configured

## Done summary
Refactored presenter architecture: renamed CodeEditorPresenter to WasmCodeEditorPresenter with platform guard, added DesktopCodeEditorPresenter shell wrapping WebView2 with security hardening (navigation allowlist, disabled script dialogs/context menus/host objects, buffered Source URI), expanded ICodeEditorPresenter with InvokeScriptAsync/PostWebMessage/MessageReceived/PresenterNavigationStartingEventArgs contracts, extracted helper interfaces (IParentAccessor, IThemeListener, IKeyboardListener, IDebugLogger), created BridgeFactory for platform-specific helper creation, added EditorLifecycleState for exactly-once event semantics with IsLoaded guards against late callbacks, replaced CodeEditorPresenter in Generic.xaml with ContentPresenter placeholder, updated OnApplyTemplate to create correct presenter via OperatingSystem.IsBrowser(), refactored WebViewExtensions to use presenter's InvokeScriptAsync, added StreamJsonRpc NuGet dependency, made ThemeListener IDisposable, and fixed ParentAccessor sanitize value flow bug.
## Evidence
- Commits: bb79bdcdf183a1d35701ea68fc24dad077e5dacb
- Tests: dotnet build MonacoEditorComponent.slnx --no-restore, dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop, dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm
- PRs: