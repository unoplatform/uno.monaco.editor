# fn-14-uno-hybridwebview-control-library.1 Project scaffold and build infrastructure

## Description
Create the `HybridWebViewComponent` project with build infrastructure, NuGet packaging, and license attribution. This is the foundation all other tasks build on.

**Size:** M
**Files:** `HybridWebViewComponent/HybridWebViewComponent.csproj`, `MonacoEditorComponent.slnx`, `ThirdPartyNotices.txt`, `Directory.Packages.props`

## Approach

- Create `HybridWebViewComponent/` directory at repo root (sibling to `MonacoEditorComponent/`)
- Model the `.csproj` after `MonacoEditorComponent/MonacoEditorComponent.csproj`:
  - `Microsoft.NET.Sdk` (NOT `Uno.Sdk`)
  - `<TargetFrameworks>net10.0</TargetFrameworks>` (single TFM)
  - `GenerateLibraryLayout=true` for NuGet packaging
  - Package metadata: `Uno.HybridWebView` package ID, description, tags
  - Reference `Uno.WinUI` via CPM from `Directory.Packages.props`
- Add project to `MonacoEditorComponent.slnx`
- Add MAUI MIT attribution to `ThirdPartyNotices.txt` (extend existing file):
  - .NET Foundation and Contributors, MIT license
  - Reference: https://github.com/dotnet/maui/blob/main/LICENSE.TXT
- Verify project builds: `dotnet build HybridWebViewComponent/HybridWebViewComponent.csproj`

## Key context

- `Directory.Build.props` already sets `ImplicitUsings`, `Nullable`, `TreatWarningsAsErrors`, `UseArtifactsOutput=true`
- `Directory.Packages.props` has CPM with `Uno.WinUI 6.5.153` already listed
- The `.slnx` format uses `<Project Path="..." />` entries (see existing `MonacoEditorComponent.slnx`)
- `global.json` pins `Uno.Sdk 6.5.31` and `.NET SDK 10.0.100`
## Acceptance
- [ ] `HybridWebViewComponent/HybridWebViewComponent.csproj` exists with correct SDK, TFM, NuGet config
- [ ] Project added to `MonacoEditorComponent.slnx`
- [ ] `Uno.WinUI` referenced via CPM (no explicit version in csproj)
- [ ] `ThirdPartyNotices.txt` updated with dotnet/maui MIT attribution
- [ ] `dotnet build HybridWebViewComponent/HybridWebViewComponent.csproj` succeeds
- [ ] `dotnet build MonacoEditorComponent.slnx` succeeds (full solution)
## Done summary
- Task completed
## Evidence
- Commits:
- Tests:
- PRs: