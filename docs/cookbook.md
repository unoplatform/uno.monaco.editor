# API Cookbook

Recipe-style examples for common `Uno.Monaco.Editor` scenarios. Each recipe is self-contained -- copy the code into your `EditorLoaded` handler (or the indicated event).

All examples assume a page with a `CodeEditor` control named `Editor`:

```xml
<Page xmlns:monaco="using:Monaco">
    <monaco:CodeEditor x:Name="Editor"
                       CodeLanguage="csharp"
                       VerticalAlignment="Stretch"
                       HorizontalAlignment="Stretch" />
</Page>
```

Required namespaces (add as needed per recipe):

```csharp
using Monaco;
using Monaco.Editor;
using Monaco.Languages;
using Monaco.Helpers;
```

---

## Table of Contents

1. [Set Text and Language](#1-set-text-and-language)
2. [Listen to Content Changes](#2-listen-to-content-changes)
3. [Configure Editor Options](#3-configure-editor-options)
4. [Register a Completion Provider](#4-register-a-completion-provider)
5. [Register a Hover Provider](#5-register-a-hover-provider)
6. [Add Line Decorations](#6-add-line-decorations)
7. [Set Diagnostic Markers](#7-set-diagnostic-markers)
8. [Register a Custom Action](#8-register-a-custom-action)
9. [Register a Keybinding Command](#9-register-a-keybinding-command)
10. [Get and Set Cursor Position](#10-get-and-set-cursor-position)
11. [Navigate to a Line](#11-navigate-to-a-line)
12. [Handle Link Clicks](#12-handle-link-clicks)
13. [Register a Code Lens Provider](#13-register-a-code-lens-provider)
14. [Register a Color Provider](#14-register-a-color-provider)
15. [Work with the Text Model](#15-work-with-the-text-model)

---

## 1. Set Text and Language

Set the editor content and syntax language at runtime.

**When:** `EditorLoaded`

```csharp
private void Editor_Loaded(object sender, RoutedEventArgs e)
{
    // Set content
    Editor.Text = "function greet(name) {\n    return `Hello, ${name}!`;\n}";

    // Change language (triggers syntax highlighting update)
    Editor.CodeLanguage = "javascript";
}
```

The `CodeLanguage` property accepts any [Monaco language identifier](https://microsoft.github.io/monaco-editor/typedoc/modules/editor_editor_api.languages.html) such as `"csharp"`, `"javascript"`, `"python"`, `"xml"`, `"json"`, etc.

You can also determine the language from a file extension:

```csharp
Editor.CodeLanguage = Editor.Languages.GetCodeLanguageFromExtension("Program.cs"); // returns "csharp"
```

An identifier Monaco does not know falls back to `plaintext` silently rather than throwing, so an unhighlighted editor -- not an error -- is the symptom of a misspelled or unavailable language. `GetLanguagesAsync()` reports what is actually registered.

### Highlighting diffs

Monaco ships no `diff` grammar of its own: VS Code's diff highlighting comes from a built-in extension carrying a TextMate grammar, and Monaco only supports Monarch. This component bundles a Monarch diff grammar and registers it at startup, so `diff` behaves like any language Monaco does ship -- it appears in `GetLanguagesAsync()`, and `.diff` / `.patch` resolve through `GetCodeLanguageFromExtension` and the `FileExtension` property.

```csharp
Editor.CodeLanguage = "diff";
Editor.Text = await File.ReadAllTextAsync(patchPath);
```

The grammar covers unified/git (`diff -u`), context (`diff -c`), normal (`diff`), and combined (`diff --cc`, merge diffs) output, including hunk ranges, git extended headers, and `\ No newline at end of file`.

Its colors are inherited from whichever built-in theme is active rather than hard-coded, so the editor stays consistent across `vs`, `vs-dark`, `hc-black`, and `hc-light`. That keeps the component from repainting themes the host application owns, at the cost of not exactly reproducing VS Code's green/red diff palette. A custom Monaco theme can target the emitted token types to change that:

| Diff content | Token type |
|---|---|
| Inserted lines (`+`, `>`) | `comment.insert.diff` |
| Deleted lines (`-`, `<`) | `string.delete.diff` |
| Changed lines (`!`) | `keyword.change.diff` |
| File and git headers | `type.header.diff` |
| Hunk ranges (`@@`, `1,2c3,4`) | `keyword.flow.range.diff` |
| Separators, `\ No newline` | `type.meta.diff` |

Defining a theme is a JavaScript-side operation (`monaco.editor.defineTheme`); there is no C# API for it today.

**Platform support:** WASM and Desktop.

---

## 2. Listen to Content Changes

React to text changes using `INotifyPropertyChanged` or two-way binding.

**Option A: PropertyChanged event**

```csharp
public MainPage()
{
    this.InitializeComponent();
    Editor.PropertyChanged += Editor_PropertyChanged;
}

private void Editor_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(Editor.Text))
    {
        // Editor.Text contains the updated content
        System.Diagnostics.Debug.WriteLine($"Content changed: {Editor.Text.Length} chars");
    }
}
```

**Option B: Two-way XAML binding**

```xml
<monaco:CodeEditor x:Name="Editor"
                   Text="{x:Bind CodeContent, Mode=TwoWay}" />
<TextBlock Text="{x:Bind CodeContent, Mode=OneWay}" />
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

**Platform support:** WASM and Desktop.

---

## 3. Configure Editor Options

Customize editor appearance and behavior through `StandaloneEditorConstructionOptions`.

**When:** Before or after `EditorLoaded`. Property changes are forwarded to Monaco automatically.

```csharp
// Set options via the existing Options instance (changes are auto-forwarded)
Editor.Options.FontSize = 16;
Editor.Options.WordWrap = "on";
Editor.Options.RenderWhitespace = "all";
Editor.Options.LineNumbers = "on";
Editor.Options.RoundedSelection = true;
Editor.Options.ScrollBeyondLastLine = false;

// Toggle minimap
Editor.Options.Minimap = new EditorMinimapOptions { Enabled = false };

// Toggle code folding
Editor.Options.Folding = true;
```

**XAML binding alternative:**

```xml
<monaco:CodeEditor x:Name="Editor"
                   Options="{x:Bind editorOptions}" />
```

```csharp
private readonly StandaloneEditorConstructionOptions editorOptions = new()
{
    FontSize = 14,
    Minimap = new EditorMinimapOptions { Enabled = false },
    WordWrap = "on"
};
```

**Platform support:** WASM and Desktop.

---

## 4. Register a Completion Provider

Provide custom IntelliSense suggestions for a language.

**When:** `EditorLoaded` (the editor must be fully initialized before provider registration).

**Step 1: Implement `CompletionItemProvider`**

```csharp
using Monaco;
using Monaco.Editor;
using Monaco.Languages;

public class MyCompletionProvider : CompletionItemProvider
{
    public string[] TriggerCharacters => ["."];

    public async Task<CompletionList> ProvideCompletionItemsAsync(
        IModel document, Position position, CompletionContext context)
    {
        return new CompletionList
        {
            Suggestions =
            [
                new CompletionItem("Console", "Console", CompletionItemKind.Class),
                new CompletionItem("WriteLine", "WriteLine", CompletionItemKind.Method),
                new CompletionItem("foreach",
                    "foreach (var ${2:item} in ${1:collection})\n{\n\t$0\n}",
                    CompletionItemKind.Snippet)
                {
                    InsertTextRules = CompletionItemInsertTextRule.InsertAsSnippet
                }
            ]
        };
    }

    public Task<CompletionItem> ResolveCompletionItemAsync(
        IModel model, CompletionItem item)
    {
        return Task.FromResult(item);
    }
}
```

**Step 2: Register the provider**

```csharp
private async void Editor_Loaded(object sender, RoutedEventArgs e)
{
    await Editor.Languages.RegisterCompletionItemProviderAsync("csharp", new MyCompletionProvider());
}
```

**Key concepts:**
- `TriggerCharacters` defines which characters invoke the completion popup.
- Use `CompletionItemInsertTextRule.InsertAsSnippet` for snippet syntax (`$1`, `$2`, `$0` for tab stops).
- `ResolveCompletionItemAsync` is called when a suggestion is selected, allowing lazy detail loading.

**Platform support:** WASM and Desktop.

---

## 5. Register a Hover Provider

Show tooltip information when the user hovers over tokens.

**When:** `EditorLoaded`

**Step 1: Implement `HoverProvider`**

```csharp
using Monaco;
using Monaco.Editor;
using Monaco.Languages;

public class MyHoverProvider : HoverProvider
{
    public async Task<Hover?> ProvideHover(IModel model, Position position)
    {
        var word = await model.GetWordAtPositionAsync(position);
        if (word?.Word?.Equals("Console", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new Hover(
                [
                    "**System.Console**",
                    "Represents the standard input, output, and error streams.",
                    "[Documentation](https://learn.microsoft.com/dotnet/api/system.console)"
                ],
                new Monaco.Range(
                    position.LineNumber, word.StartColumn,
                    position.LineNumber, word.EndColumn));
        }

        return null;
    }
}
```

**Step 2: Register**

```csharp
private async void Editor_Loaded(object sender, RoutedEventArgs e)
{
    await Editor.Languages.RegisterHoverProviderAsync("csharp", new MyHoverProvider());
}
```

**Key concepts:**
- Return `null` when no hover information is available for the position.
- Hover content strings support Markdown formatting.
- The `Range` parameter highlights the hovered word.

**Platform support:** WASM and Desktop.

---

## 6. Add Line Decorations

Highlight lines, add glyph margin icons, and apply inline text styles.

**When:** `EditorLoaded`

```csharp
using Microsoft.UI;
using Microsoft.UI.Text;
using Monaco.Editor;
using Monaco.Helpers;
using Windows.UI.Text;

private void Editor_Loaded(object sender, RoutedEventArgs e)
{
    // Highlight a range with a red background and bold white text
    Editor.Decorations.Add(
        new IModelDeltaDecoration(
            new Monaco.Range(3, 1, 3, 10),
            new IModelDecorationOptions
            {
                ClassName = new CssLineStyle { BackgroundColor = Colors.DarkRed },
                InlineClassName = new CssInlineStyle
                {
                    ForegroundColor = Colors.White,
                    FontWeight = FontWeights.Bold
                },
                HoverMessage = new[] { "This line has an issue." }.ToMarkdownString(),
                Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges
            }));

    // Highlight an entire line with a glyph margin icon
    Editor.Decorations.Add(
        new IModelDeltaDecoration(
            new Monaco.Range(5, 1, 5, 1),
            new IModelDecorationOptions
            {
                IsWholeLine = true,
                ClassName = new CssLineStyle { BackgroundColor = Colors.AliceBlue },
                GlyphMarginClassName = new CssGlyphStyle
                {
                    GlyphImage = new System.Uri("ms-appx-web:///Icons/error.png")
                },
                GlyphMarginHoverMessage = new[] { "Error on this line." }.ToMarkdownString()
            }));
}
```

**Clear all decorations:**

```csharp
Editor.Decorations.Clear();
```

**Key concepts:**
- `CssLineStyle` controls the line background color.
- `CssInlineStyle` controls inline text appearance (color, weight, style, decoration).
- `CssGlyphStyle` adds an icon to the glyph margin (set `HasGlyphMargin="True"` on the editor).
- Hover messages support Markdown via the `ToMarkdownString()` extension method.
- `Decorations` is an `IObservableVector` -- changes are automatically synced to Monaco.

**Platform support:** WASM and Desktop.

---

## 7. Set Diagnostic Markers

Display errors, warnings, and information markers in the editor (squiggly underlines in the gutter and minimap).

**When:** `EditorLoaded`

**Option A: Observable collection (data-binding friendly)**

```csharp
Editor.Markers.Add(new MarkerData
{
    Code = "CS0219",
    Message = "Variable 'x' is assigned but never used.",
    Severity = MarkerSeverity.Warning,
    Source = "MyAnalyzer",
    StartLineNumber = 5,
    StartColumn = 9,
    EndLineNumber = 5,
    EndColumn = 10
});

Editor.Markers.Add(new MarkerData
{
    Code = "CS1002",
    Message = "Expected ';'",
    Severity = MarkerSeverity.Error,
    Source = "MyAnalyzer",
    StartLineNumber = 8,
    StartColumn = 15,
    EndLineNumber = 8,
    EndColumn = 16
});
```

**Option B: Direct API call**

```csharp
await Editor.SetModelMarkersAsync("MyAnalyzer",
[
    new MarkerData
    {
        Code = "CS0219",
        Message = "Variable 'x' is assigned but never used.",
        Severity = MarkerSeverity.Warning,
        Source = "MyAnalyzer",
        StartLineNumber = 5,
        StartColumn = 9,
        EndLineNumber = 5,
        EndColumn = 10
    }
]);
```

**Clear all markers:**

```csharp
await Editor.SetModelMarkersAsync("MyAnalyzer", []);
```

**Key concepts:**
- Use either the `Markers` property or the `SetModelMarkersAsync` method, not both simultaneously.
- `MarkerSeverity` values: `Error`, `Warning`, `Info`, `Hint`.
- The `owner` string in `SetModelMarkersAsync` groups markers for clearing.

**Platform support:** WASM and Desktop.

---

## 8. Register a Custom Action

Add a custom command to the editor context menu and command palette.

**When:** `EditorLoaded`

**Step 1: Implement `IActionDescriptor`**

```csharp
using Monaco;
using Monaco.Editor;

public class FormatAction : IActionDescriptor
{
    public string Id => "my-format-action";
    public string? Label => "Format Selection";
    public string? ContextMenuGroupId => "1_modification";
    public float ContextMenuOrder => 1.5f;
    public int[] Keybindings => [KeyMod.CtrlCmd | KeyMod.Shift | KeyCode.KEY_F];
    public string? Precondition => null;
    public string? KeybindingContext => null;

    public async void Run(CodeEditorBase editor, object[]? args)
    {
        var selected = editor.SelectedText;
        // Process the selected text...
        editor.SelectedText = selected.ToUpperInvariant();
        editor.Focus(FocusState.Programmatic);
    }
}
```

**Step 2: Register**

```csharp
private async void Editor_Loaded(object sender, RoutedEventArgs e)
{
    await Editor.AddActionAsync(new FormatAction());
}
```

**Key concepts:**
- `ContextMenuGroupId` places the action in the context menu: `"navigation"`, `"1_modification"`, or `"9_cutcopypaste"`.
- Use `KeyMod.Chord(first, second)` for two-key chord bindings (e.g., Ctrl+K followed by Ctrl+M).
- Always call `AddActionAsync` after `EditorLoaded` fires to ensure the bridge is initialized.

**Platform support:** WASM and Desktop.

---

## 9. Register a Keybinding Command

Bind a keyboard shortcut to a callback without adding a context menu entry.

**When:** `EditorLoaded`

```csharp
private async void Editor_Loaded(object sender, RoutedEventArgs e)
{
    // Simple keybinding
    await Editor.AddCommandAsync(KeyCode.F5, (args) =>
    {
        System.Diagnostics.Debug.WriteLine("F5 pressed!");
    });

    // Modifier + key
    await Editor.AddCommandAsync(KeyMod.CtrlCmd | KeyCode.KEY_R, (args) =>
    {
        System.Diagnostics.Debug.WriteLine("Ctrl+R pressed!");
    });

    // Conditional command using context keys
    var canRun = await Editor.CreateContextKeyAsync("CanRun", false);

    await Editor.AddCommandAsync(KeyCode.F5, (args) =>
    {
        System.Diagnostics.Debug.WriteLine("F5 pressed (gated)!");
        canRun.Reset(); // Disable the command after use
    }, canRun.Key);

    // Later, enable the command:
    canRun.Set(true);
}
```

**Key concepts:**
- `AddCommandAsync` returns a command ID string that can be used with `CodeLensProvider` to link commands to code lenses.
- `CommandHandler` receives `object?[]` parameters. Arguments are deserialized as `System.Text.Json.JsonElement` instances (breaking change from the prior Newtonsoft `JObject` type). Extract values using the `JsonElement` API:

```csharp
await Editor.AddCommandAsync(KeyCode.F5, (args) =>
{
    if (args.Length > 0 && args[0] is System.Text.Json.JsonElement json)
    {
        // Read properties from the JsonElement
        var value = json.GetString();
        System.Diagnostics.Debug.WriteLine($"Arg: {value}");
    }
});
```

- Use `CreateContextKeyAsync` to gate commands on boolean conditions.

**Platform support:** WASM and Desktop.

---

## 10. Get and Set Cursor Position

Read and manipulate the cursor position programmatically.

**When:** `EditorLoaded`

```csharp
// Get current position
var position = await Editor.GetPositionAsync();
if (position != null)
{
    System.Diagnostics.Debug.WriteLine($"Cursor at line {position.LineNumber}, column {position.Column}");
}

// Set position
await Editor.SetPositionAsync(new Position(10, 1));
```

**Read the selected range:**

```csharp
// SelectedRange is a dependency property (bindable)
var selection = Editor.SelectedRange;
if (selection != null)
{
    System.Diagnostics.Debug.WriteLine(
        $"Selection: ({selection.StartLineNumber},{selection.StartColumn}) to ({selection.EndLineNumber},{selection.EndColumn})");
}
```

**Set selected text:**

```csharp
// Replace the current selection with new text
Editor.SelectedText = "replacement text";
```

**Platform support:** WASM and Desktop.

---

## 11. Navigate to a Line

Scroll the editor viewport to reveal a specific line or position.

**When:** `EditorLoaded`

```csharp
// Reveal a line (scrolls to make it visible)
await Editor.RevealLineAsync(42);

// Reveal a line in the center of the viewport
await Editor.RevealLineInCenterAsync(42);

// Reveal a position (line + column)
await Editor.RevealPositionInCenterAsync(new Position(10, 5));

// Reveal a range
await Editor.RevealRangeInCenterAsync(new Monaco.Range(10, 1, 15, 1));

// Reveal only if outside the current viewport (avoids jarring scrolls)
await Editor.RevealLineInCenterIfOutsideViewportAsync(42);
```

**Platform support:** WASM and Desktop.

---

## 12. Handle Link Clicks

Control what happens when the user Ctrl+clicks a URL in the editor.

**When:** Constructor or `Loaded`

```csharp
public MainPage()
{
    this.InitializeComponent();
    Editor.OpenLinkRequested += Editor_OpenLinkRequested;
}

private void Editor_OpenLinkRequested(CodeEditorBase sender, OpenLinkRequestedEventArgs args)
{
    // Block navigation entirely
    args.Handled = true;

    // Or implement custom handling
    if (args.Uri != null)
    {
        System.Diagnostics.Debug.WriteLine($"Link clicked: {args.Uri}");
        // Open in external browser, navigate in-app, etc.
    }
}
```

**Key concepts:**
- Set `args.Handled = true` to prevent the default browser/WebView navigation.
- If not handled, the link opens in a new window (desktop) or follows default browser behavior (WASM).

**Platform support:** WASM and Desktop.

---

## 13. Register a Code Lens Provider

Display inline actionable information above code lines (similar to "N references" in Visual Studio).

**When:** `EditorLoaded`

**Step 1: Implement `CodeLensProvider`**

```csharp
using Monaco;
using Monaco.Editor;
using Monaco.Languages;

public class MyCodeLensProvider(string commandId) : CodeLensProvider
{
    public async Task<CodeLensList> ProvideCodeLensesAsync(IModel model)
    {
        return new CodeLensList
        {
            Lenses =
            [
                new CodeLens
                {
                    Id = "my-lens",
                    Range = new Monaco.Range(1, 1, 2, 1),
                    Command = new Command
                    {
                        Id = commandId,
                        Title = "Run Tests (2 passing)",
                        Arguments = ["arg1", 42],
                        Tooltip = "Click to run tests"
                    }
                }
            ]
        };
    }

    public async Task<CodeLens> ResolveCodeLensAsync(IModel model, CodeLens codeLens)
    {
        return codeLens;
    }
}
```

**Step 2: Register with a linked command**

Code lens actions require a command ID. Register a command first, then pass its ID to the code lens provider:

```csharp
private async void Editor_Loaded(object sender, RoutedEventArgs e)
{
    var cmdId = await Editor.AddCommandAsync(0, (args) =>
    {
        System.Diagnostics.Debug.WriteLine($"Code lens clicked with arg: {args[0]}");
    });

    await Editor.Languages.RegisterCodeLensProviderAsync("csharp", new MyCodeLensProvider(cmdId ?? ""));
}
```

**Platform support:** WASM and Desktop.

---

## 14. Register a Color Provider

Enable color picker support for color values in the editor.

**When:** `EditorLoaded`

**Step 1: Implement `DocumentColorProvider`**

```csharp
using Monaco;
using Monaco.Editor;
using Monaco.Languages;

public class MyColorProvider : DocumentColorProvider
{
    public async Task<IEnumerable<ColorInformation>> ProvideDocumentColorsAsync(IModel document)
    {
        var colors = new List<ColorInformation>();

        // Find hex color patterns in the document
        var matches = await document.FindMatchesAsync(
            "#[A-Fa-f0-9]{6,8}", true, true, true, null, true);

        foreach (var match in matches)
        {
            // Parse the color string and create ColorInformation entries
            if (match.Matches?.FirstOrDefault() is string colorStr && match.Range is not null)
            {
                colors.Add(new ColorInformation(
                    Microsoft.UI.Colors.Black, // Parsed color value
                    match.Range));
            }
        }

        return colors;
    }

    public async Task<IEnumerable<ColorPresentation>> ProvideColorPresentationsAsync(
        IModel document, ColorInformation colorInfo)
    {
        return
        [
            new ColorPresentation(colorInfo.Color.ToString())
        ];
    }
}
```

**Step 2: Register**

```csharp
private async void Editor_Loaded(object sender, RoutedEventArgs e)
{
    await Editor.Languages.RegisterColorProviderAsync("css", new MyColorProvider());
}
```

**Platform support:** WASM and Desktop.

---

## 15. Work with the Text Model

Access and query the underlying text model for advanced operations.

**When:** `EditorLoaded`

```csharp
private async void Editor_Loaded(object sender, RoutedEventArgs e)
{
    if (Editor.GetModel() is not { } model) return;

    // Get document content
    var fullText = await model.GetValueAsync();
    var lineCount = await model.GetLineCountAsync();
    var thirdLine = await model.GetLineContentAsync(3);

    // Get a range of text
    var range = new Monaco.Range(2, 1, 4, 10);
    var segment = await model.GetValueInRangeAsync(range);

    // Get the full document range
    var fullRange = await model.GetFullModelRangeAsync();

    // Get word at cursor position
    var position = await Editor.GetPositionAsync();
    if (position != null)
    {
        var word = await model.GetWordAtPositionAsync(position);
        if (word != null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Word: {word.Word} (columns {word.StartColumn}-{word.EndColumn})");
        }
    }

    // Search the document
    var matches = await model.FindMatchesAsync(
        "TODO", // search string
        true,   // search only editable range
        false,  // is regex
        false,  // match case
        null,   // word separators
        true);  // capture matches

    foreach (var match in matches)
    {
        System.Diagnostics.Debug.WriteLine($"Found TODO at: {match.Range}");
    }
}
```

**Platform support:** WASM and Desktop.

---

## Error Handling

Subscribe to `InternalException` for diagnostics on interop failures:

```csharp
Editor.InternalException += (sender, ex) =>
{
    System.Diagnostics.Debug.WriteLine($"Monaco error: {ex.Message}");
};
```

Use `KeyDown` to intercept keyboard input before Monaco processes it:

```csharp
Editor.KeyDown += (sender, e) =>
{
    if (e.KeyCode == 13 && e.CtrlKey) // Ctrl+Enter
    {
        e.Handled = true; // Prevent the keypress from reaching Monaco
        // Handle Ctrl+Enter...
    }
};
```

**Platform support:** WASM and Desktop.

---

## Further Reading

- **[Getting Started](getting-started.md)** -- prerequisites, installation, and first editor setup
- **[Architecture](architecture.md)** -- dual-platform interop, lifecycle, and serialization details
- **[Monaco Editor TypeDoc](https://microsoft.github.io/monaco-editor/typedoc/index.html)** -- upstream API reference
- **[Changelog](../CHANGELOG.md)** -- release history and breaking changes
