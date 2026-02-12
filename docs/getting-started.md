# Getting Started

This guide walks you through adding the `Uno.Monaco.Editor` control to a new or existing Uno Platform application targeting WebAssembly (WASM) and/or Desktop (Skia).

## Prerequisites

| Requirement | Notes |
|-------------|-------|
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Required for all targets |
| `wasm-tools` workload | Required for WASM targets (`dotnet workload install wasm-tools`) |
| [Uno Platform templates](https://platform.uno/docs/articles/getting-started.html) | `dotnet new install Uno.Templates` |
| Web host runtime | Desktop uses the Uno `WebView2` control. On Windows this requires the [Evergreen WebView2 runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/); on Linux, WebKitGTK (`libwebkit2gtk-4.1`); on macOS, the built-in WKWebView. |

## Installation

Add the NuGet package to your project:

```bash
dotnet add package Uno.Monaco.Editor
```

> **Note:** The package was renamed from `Monaco.Editor` to `Uno.Monaco.Editor`. If you are migrating from a prior version, see the [Changelog](../CHANGELOG.md) for the full list of breaking changes.

## Your First Editor (WASM)

### 1. Create a project

If you do not already have an Uno Platform app:

```bash
dotnet new unoapp -n MonacoDemo --preset=blank
cd MonacoDemo
dotnet add MonacoDemo/MonacoDemo.csproj package Uno.Monaco.Editor
```

### 2. Add the CodeEditor to XAML

Open your `MainPage.xaml` and add the `monaco` namespace and the `CodeEditor` control:

```xml
<Page x:Class="MonacoDemo.MainPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:monaco="using:Monaco">

    <monaco:CodeEditor x:Name="Editor"
                       CodeLanguage="csharp"
                       VerticalAlignment="Stretch"
                       HorizontalAlignment="Stretch" />
</Page>
```

### 3. Set initial content

In `MainPage.xaml.cs`, subscribe to `EditorLoaded` to set content once Monaco is ready:

```csharp
using Monaco;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
        Editor.EditorLoaded += Editor_Loaded;
    }

    private void Editor_Loaded(object sender, RoutedEventArgs e)
    {
        Editor.Text = "Console.WriteLine(\"Hello, Monaco!\");";
    }
}
```

### 4. Build and run

```bash
dotnet build -f net10.0-browserwasm
dotnet run -f net10.0-browserwasm
```

You should see a fully functional code editor in your browser with C# syntax highlighting.

## Your First Editor (Desktop)

Desktop uses the same XAML and C# code as WASM. The only difference is the target framework.

### Build and run

```bash
dotnet build -f net10.0-desktop
dotnet run -f net10.0-desktop
```

On desktop, the editor runs inside the Uno `WebView2` control. Windows uses the Chromium-based WebView2 runtime; macOS uses WKWebView; Linux uses WebKitGTK.

> **Platform note:** `AddActionAsync` and `AddCommandAsync` throw `PlatformNotSupportedException` on desktop because they require JSExport callbacks. See the [platform matrix](../README.md#platform-support) for the full list of platform differences.

## Editor Lifecycle

The editor fires two lifecycle events in order:

1. **`EditorLoading`** -- The editor infrastructure is initializing. Monaco is not yet available. Register language providers here.
2. **`EditorLoaded`** -- Monaco is fully initialized and ready for interaction. Safe to read/write content, set cursor positions, and interact with the model.

```csharp
public MainPage()
{
    this.InitializeComponent();
    Editor.EditorLoading += Editor_Loading;
    Editor.EditorLoaded += Editor_Loaded;
}

private async void Editor_Loading(object sender, RoutedEventArgs e)
{
    // Register providers before the editor is fully loaded
    await Editor.Languages.RegisterCompletionItemProviderAsync("csharp", new MyCompletionProvider());
    await Editor.Languages.RegisterHoverProviderAsync("csharp", new MyHoverProvider());
}

private void Editor_Loaded(object sender, RoutedEventArgs e)
{
    // Set content after the editor is ready
    Editor.Text = "// Start typing...";
}
```

See [architecture.md](architecture.md#lifecycle-state-machine) for the full lifecycle state machine diagram.

## Common Configuration

### Editor Options

Editor behavior is controlled through `StandaloneEditorConstructionOptions`. You can set options declaratively via XAML binding or imperatively in code.

**XAML binding (recommended):**

```xml
<monaco:CodeEditor x:Name="Editor"
                   CodeLanguage="javascript"
                   Options="{x:Bind editorOptions}" />
```

```csharp
private readonly StandaloneEditorConstructionOptions editorOptions = new()
{
    FontSize = 14,
    Minimap = new EditorMinimapOptions { Enabled = false },
    WordWrap = "on",
    Folding = true,
    RenderWhitespace = "all"
};
```

**Imperative updates:**

Individual property changes on the existing `Options` instance are automatically forwarded to Monaco. You do not need to re-assign the entire `Options` object:

```csharp
// Toggle minimap at runtime
Editor.Options.Minimap = new EditorMinimapOptions
{
    Enabled = !Editor.Options.Minimap?.Enabled ?? false
};
```

### Read-Only Mode

Toggle read-only mode with the `ReadOnly` property:

```csharp
Editor.ReadOnly = true;  // Prevent editing
Editor.ReadOnly = false; // Allow editing
```

### Theme Switching

The editor automatically adapts to the system light/dark/high-contrast theme. You can also override the theme per-control:

```csharp
// Follow system theme (default)
Editor.RequestedTheme = ElementTheme.Default;

// Force light theme
Editor.RequestedTheme = ElementTheme.Light;

// Force dark theme
Editor.RequestedTheme = ElementTheme.Dark;
```

### Two-Way Text Binding

`CodeEditor.Text` is a dependency property that supports two-way binding:

```xml
<monaco:CodeEditor x:Name="Editor"
                   CodeLanguage="csharp"
                   Text="{x:Bind CodeContent, Mode=TwoWay}" />
```

```csharp
public string CodeContent
{
    get => (string)GetValue(CodeContentProperty);
    set => SetValue(CodeContentProperty, value);
}

public static readonly DependencyProperty CodeContentProperty =
    DependencyProperty.Register(nameof(CodeContent), typeof(string), typeof(MainPage), new PropertyMetadata(""));
```

### Language Detection

Determine the Monaco language identifier from a file extension or filename:

```csharp
// All of these return "csharp"
string lang1 = Editor.Languages.GetCodeLanguageFromExtension("cs");
string lang2 = Editor.Languages.GetCodeLanguageFromExtension(".cs");
string lang3 = Editor.Languages.GetCodeLanguageFromExtension("Program.cs");
```

## Troubleshooting

### Editor shows a blank area

- Ensure the `CodeEditor` has explicit or stretch sizing. A zero-width or zero-height layout produces a blank area.
- Verify that `install-dependencies.ps1` has been run if building from source, so the Monaco distribution is present.

### Monaco does not load on desktop

- **Windows:** Verify the [WebView2 runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) is installed. The Evergreen bootstrapper installs it automatically on most systems.
- **Linux:** The Uno `WebView2` control requires WebKitGTK on Linux. Install it via your package manager (e.g., `sudo apt install libwebkit2gtk-4.1-dev` on Ubuntu). macOS uses the built-in WKWebView and requires no additional setup.

### `PlatformNotSupportedException` on desktop

`AddActionAsync` and `AddCommandAsync` are WASM-only. Guard calls with a platform check:

```csharp
if (OperatingSystem.IsBrowser())
{
    await Editor.AddActionAsync(new MyAction());
}
```

### Content set before `EditorLoaded` does not appear

The `Text` property can be set at any time (including before the editor loads). The control applies all pending property values during initialization. If you set `Text` in your constructor or `Loaded` handler and it does not appear, verify you are not overwriting it in `EditorLoaded`.

### Editor does not respond to input

Check that the editor has keyboard focus. Use `Editor.Focus(FocusState.Programmatic)` to explicitly focus the editor after modal dialogs or other focus-stealing operations.

## Next Steps

- **[API Cookbook](cookbook.md)** -- 12 recipe-style examples for common scenarios
- **[Architecture](architecture.md)** -- deep dive into the dual-platform interop model
- **[Monaco Editor documentation](https://microsoft.github.io/monaco-editor/typedoc/index.html)** -- upstream TypeScript API reference
