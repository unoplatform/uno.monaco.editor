# Uno.Monaco.Editor

A cross-platform [Uno Platform](https://platform.uno/) wrapper around the [Monaco Editor](https://microsoft.github.io/monaco-editor/), bringing the same code editor that powers VS Code to .NET applications targeting WebAssembly and Desktop (Skia).

[![NuGet](https://img.shields.io/nuget/v/Uno.Monaco.Editor?style=flat-square)](https://www.nuget.org/packages/Uno.Monaco.Editor)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](LICENSE)
[![CI](https://img.shields.io/github/actions/workflow/status/unoplatform/uno.monaco.editor/ci.yml?branch=main&style=flat-square&label=CI)](https://github.com/unoplatform/uno.monaco.editor/actions/workflows/ci.yml)

> This project is not affiliated with the Monaco team and is provided for convenience. Please direct issues related to this control wrapper to this repository.

## Key Features

- **IntelliSense and completions** -- register custom completion providers with snippet support
- **Syntax highlighting** -- set the code language to get full Monaco syntax highlighting
- **Themes** -- automatic light/dark/high-contrast theme switching based on system settings
- **Decorations and markers** -- strongly-typed C# abstractions for line decorations and diagnostic markers
- **Language providers** -- CodeAction, CodeLens, Color, Completion, and Hover provider bridges
- **Actions and commands** -- register custom editor actions and keybinding commands (WASM)
- **Editor options** -- full `StandaloneEditorConstructionOptions` support through `CodeEditor.Options`
- **Two-way text binding** -- bind editor content to C# properties with change notifications
- **Dual-platform support** -- single codebase runs on both `net10.0-browserwasm` and `net10.0-desktop`

## Platform Support

| Feature | WASM (browserwasm) | Desktop (Windows/macOS/Linux) |
|---------|:------------------:|:-----------------------------:|
| Text editing and syntax highlighting | Supported | Supported |
| Editor options and themes | Supported | Supported |
| Decorations and markers | Supported | Supported |
| Language providers (Completion, Hover, etc.) | Supported | Supported |
| `AddActionAsync` / `AddCommandAsync` | Supported | Not supported |
| `PostWebMessage` | Not supported | Supported |
| Interop mechanism | JSImport / JSExport | JSON-RPC over WebView2 |

`AddActionAsync` and `AddCommandAsync` require JSExport callbacks and throw `PlatformNotSupportedException` on desktop. See [architecture docs](docs/architecture.md#platform-asymmetric-apis) for details.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) with the `wasm-tools` workload (for WASM targets)
- [Uno Platform project](https://platform.uno/docs/articles/getting-started.html) targeting `net10.0-browserwasm` and/or `net10.0-desktop`

### Install

Add the NuGet package to your project:

```
dotnet add package Uno.Monaco.Editor
```

### Minimal Example

**XAML:**

```xml
<Page x:Class="MyApp.MainPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:monaco="using:Monaco">

    <monaco:CodeEditor x:Name="Editor"
                       CodeLanguage="csharp"
                       VerticalAlignment="Stretch"
                       HorizontalAlignment="Stretch" />
</Page>
```

**C# code-behind:**

```csharp
using Monaco;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
        Editor.EditorLoaded += Editor_Loaded;
    }

    private async void Editor_Loaded(object sender, RoutedEventArgs e)
    {
        // Set initial content
        Editor.Text = "Console.WriteLine(\"Hello, Monaco!\");";

        // Configure editor options
        await Editor.UpdateOptionsAsync(new StandaloneEditorConstructionOptions
        {
            FontSize = 14,
            Minimap = new EditorMinimapOptions { Enabled = false }
        });
    }
}
```

## Usage Overview

- **[Architecture](docs/architecture.md)** -- internal design, dual-platform interop, lifecycle state machine, and serialization layer
- **[Changelog](CHANGELOG.md)** -- release history, breaking changes, and migration guide
- **[Getting Started Guide](docs/getting-started.md)** -- step-by-step tutorials for WASM and Desktop targets
- **[API Cookbook](docs/cookbook.md)** -- common scenarios: set text/language, listen to changes, register providers, add decorations

The `MonacoEditorTestApp` project in this repository provides a working playground with examples of text binding, language providers, decorations, markers, and theme switching.

### Monaco API Conventions

The C# API follows Monaco/TypeScript naming as closely as possible while using idiomatic C#/WinRT types:

- All interop methods are asynchronous and end with the `Async` suffix.
- Language APIs are accessed through the editor instance: `editor.Languages.RegisterCompletionItemProviderAsync(...)` (there is no global `monaco.languages` equivalent because each editor instance hosts its own WebView context).
- `CommandHandler` delegates receive `System.Text.Json.JsonElement` (not Newtonsoft `JObject`).

## Build from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) with the `wasm-tools` workload
- [Node.js](https://nodejs.org/) (for TypeScript compilation and Monaco dependencies)
- [PowerShell](https://github.com/PowerShell/PowerShell) (for `install-dependencies.ps1`)

### Steps

```bash
# 1. Install Monaco and build TypeScript bundles
pwsh ./install-dependencies.ps1

# 2. Restore and build the solution
dotnet restore MonacoEditorComponent.slnx
dotnet build MonacoEditorComponent.slnx --no-restore

# 3. Build the test app for specific targets
dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm
dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-desktop
```

### Type Generation Pipeline

The C# API surface in `MonacoEditorComponent/Monaco/` is generated from Monaco's TypeScript definitions using a two-stage pipeline:

1. **ts-morph extractor** (`tools/monaco-type-extractor/`): Parses `monaco.d.ts` into a versioned intermediate JSON model.
2. **.NET CLI emitter** (`tools/MonacoTypeEmitter/`): Emits C# classes with System.Text.Json attributes from the intermediate model.

To regenerate after updating the Monaco version:

```bash
npx tsx tools/monaco-type-extractor/src/index.ts -- node_modules/monaco-editor/monaco.d.ts \
    -o tools/monaco-type-extractor/output/model.json
dotnet run --project tools/MonacoTypeEmitter -- \
    --input tools/monaco-type-extractor/output/model.json \
    --output MonacoEditorComponent/Monaco/
```

## Monaco Version

This package bundles **Monaco Editor 0.52.2** (declared as `^0.52.2` in `package.json`).

## Breaking Changes

See the [Changelog](CHANGELOG.md) for the full list of breaking changes and a step-by-step migration guide from `Monaco.Editor` 2.0.0-dev.60 to `Uno.Monaco.Editor`.

## Contributing

See [AGENTS.md](AGENTS.md) for development workflow, code conventions, and commit guidelines.

## License

This project is licensed under the [MIT License](LICENSE).
