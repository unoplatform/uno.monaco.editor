## Description

Consolidate the library to single `net10.0` TFM, replace all preprocessor-based platform detection with runtime checks, migrate to DispatcherQueue, fix known bugs, and prepare build infrastructure.

**Size:** M
**Files:** MonacoEditorComponent/MonacoEditorComponent.csproj, MonacoEditorComponent/CodeEditor/ICodeEditorPresenter.cs, MonacoEditorComponent/CodeEditor/CodeEditor.cs, MonacoEditorComponent/CodeEditor/CodeEditor.Properties.cs, MonacoEditorComponent/CodeEditor/CodeEditor.Events.cs, MonacoEditorComponent/CodeEditor/CodeEditorPresenter.wasm.cs, MonacoEditorComponent/Helpers/DispatcherTaskExtensions.cs, MonacoEditorComponent/Helpers/ThemeListener.cs, global.json

## Approach

- **csproj**: Change `<TargetFrameworks>net9.0;net10.0</TargetFrameworks>` to `<TargetFrameworks>net10.0</TargetFrameworks>`. CRITICAL: keep `TargetFrameworks` (plural) — Uno requires this even for single TFM. Remove unconditional `<DefineConstants>$(DefineConstants);__WASM__</DefineConstants>` (`MonacoEditorComponent.csproj:10`). Update Title/Description/PackageTags to remove WASM-only framing.
- **Runtime detection migration**: Replace ALL `#if __WASM__` / `#if !__WASM__` with `OperatingSystem.IsBrowser()` **in `MonacoEditorComponent/` only**. Known locations include `CodeEditor.cs:145`, `CodeEditor.cs:389`, `CodeEditor.Events.cs:52`, `CodeEditor.Events.cs:159`, `ThemeListener.cs:43`. Run repo-wide grep to find any others in the library. Note: `MonacoEditorTestApp/` may retain platform-specific `#if` in `App.xaml.cs` and platform startup files — this is standard Uno practice for app projects.
- **DispatcherQueue migration**: Change `ICodeEditorPresenter.Dispatcher` from `CoreDispatcher` to `DispatcherQueue`. Migrate all `Dispatcher.RunAsync` callsites to `DispatcherQueue.TryEnqueue()`. Update or remove `DispatcherTaskExtensions.cs`.
- **IsEditorLoadedProperty fix**: DP registered as `typeof(string)` but used as `bool`. Correct the type.
- **RenderingBackend property**: Read-only DP, enum `Wasm`/`Desktop`, set based on `OperatingSystem.IsBrowser()`.
- **global.json**: Add `"test": { "runner": "Microsoft.Testing.Platform" }` for MTP2 support. This task owns adding it; Task 6 consumes it.

## Key context

- **Task 1 is the foundation**: All subsequent tasks depend on the TFM consolidation, preprocessor removal, and DispatcherQueue migration completed here. Current code still has `net9.0`, `__WASM__` defines, `#if __WASM__` guards, and `CoreDispatcher` usage — all addressed by this task.
- `OperatingSystem.IsBrowser()` is the canonical Uno pattern for single-TFM libraries
- `.wasm.cs`/`.desktop.cs` file suffixes have NO compile-time meaning in single-TFM libraries
- `[JSImport]`/`[JSExport]` attributes compile fine in `net10.0` but only function at runtime on WASM

## Acceptance

- [ ] `TargetFrameworks` is `net10.0` (single TFM, plural property name)
- [ ] No `__WASM__` define in csproj
- [ ] No `#if __WASM__` or `#if __DESKTOP__` in `MonacoEditorComponent/` (verified by `grep -r "#if.*__WASM__\|#if.*__DESKTOP__" MonacoEditorComponent/`)
- [ ] `ICodeEditorPresenter.Dispatcher` is `DispatcherQueue`
- [ ] All `Dispatcher.RunAsync()` callsites migrated to `DispatcherQueue.TryEnqueue()`
- [ ] `IsEditorLoadedProperty` DP type corrected to `bool`
- [ ] `RenderingBackend` property exposed
- [ ] `global.json` has `"test": { "runner": "Microsoft.Testing.Platform" }`
- [ ] `dotnet build MonacoEditorComponent.slnx` succeeds
- [ ] MonacoEditorTestApp builds for both `net10.0-browserwasm` and `net10.0-desktop`

## Done summary
Consolidated library TFM from net9.0;net10.0 to single net10.0, replaced all #if __WASM__ preprocessor directives with OperatingSystem.IsBrowser() runtime checks, migrated CoreDispatcher to DispatcherQueue throughout, fixed IsEditorLoadedProperty DP type from string to bool, added RenderingBackend enum and read-only dependency property, and added MTP2 test runner config to global.json.
## Evidence
- Commits: ad1b504, 3e93b57
- Tests: dotnet build MonacoEditorComponent.slnx --no-restore, dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm, dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop
- PRs: