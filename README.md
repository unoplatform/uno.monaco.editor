Monaco Editor for Uno Platform
==============================
An [Uno Platform](https://platform.uno/) wrapper around the web-based [Monaco Editor](https://microsoft.github.io/monaco-editor/).  This allows the Monaco Editor to be consumed directly in XAML for C# projects targeting WebAssembly (browserwasm) and Desktop (Skia).

This project is not affiliated with the Monaco team and is provided for convenience.  Please direct issues related to the use of this control wrapper to this repository.

This control is still in an early alpha state.  Currently, every minor version change may signal breaking changes.


Supported Features
------------------
The following Monaco Editor features are currently supported by this component bridge:

- Two-way Text Binding
- Code Language for Syntax Highlighting
- Stand Alone Code [Editor Options](https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.ieditoroptions.html)
- [Markers](https://microsoft.github.io/monaco-editor/api/modules/monaco.editor.html#setmodelmarkers) and [Decorations](https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.imodeldeltadecoration.html) (See #35)
- [Actions](https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.istandalonecodeeditor.html#addaction) and [Commands](https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.istandalonecodeeditor.html#addcommand)
- Basic [Language Features](https://code.visualstudio.com/api/language-extensions/programmatic-language-features)
  - [CodeAction](https://microsoft.github.io/monaco-editor/api/modules/monaco.languages.html#registercodeactionprovider) (Commands and/or Edits)
  - [CodeLens](https://microsoft.github.io/monaco-editor/api/modules/monaco.languages.html#registercodelensprovider) (onDidChange not supported)
  - [Color](https://microsoft.github.io/monaco-editor/api/modules/monaco.languages.html#registercolorprovider)
  - [CompletionItem](https://microsoft.github.io/monaco-editor/api/modules/monaco.languages.html#registercompletionitemprovider) (IntelliSense, Snippets)
  - [Hover](https://microsoft.github.io/monaco-editor/api/modules/monaco.languages.html#registerhoverprovider)
- Basic [ITextModel](https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.itextmodel.html) Support (including [FindMatches](https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.itextmodel.html#findmatches))
- KeyDown Events
- Responds Appropriately to Programmatic Focus Events
- Render Aware: Only displays once Loading is complete.
- Out-of-process WebView usage.

Usage
-----

A NuGet Package is provided:

```
Install-Package Monaco.Editor
```

Look at the TestApp for current usage and basic examples.
See [changelog](changelog.md) for more info.


Monaco API Notes
----------------
This project maintains two tenants with regards to the core web-based [Monaco API](https://microsoft.github.io/monaco-editor/api/index.html):

  1. Keep API names and patterns as closely mapped as possible to enable straight-forward re-usage of existing TypeScript examples.
  2. Swap types to existing C#/WinRT based ones for easier interop with the calling application.

Effectively, we want this project to be as easy to integrate with existing C#/WinRT skills/knowledge as possible as well as providing a similar enough API that it's easy to still utilize any existing knowledge bases for the Monaco API.

There are some common caveats though called out here:

  - Pretty much all functions are asynchronous and end with the C# `Async` naming suffix convention.
  - The `Monaco.Languages` namespace is mapped through the `CodeEditor` instance, so call `<Editor Instance>.Languages.*` for any of [those APIs](https://microsoft.github.io/monaco-editor/api/modules/monaco.languages.html). E.g.

    ```javascript
    // JavaScript
    monaco.languages.registerColorProvider("colorLanguage", ...
    ```

    ```csharp
    // C#
    var MonacoEditor = new CodeEditor(); // Either done through XAML or elsewhere once.
    ...
    MonacoEditor.Languages.RegisterColorProviderAsync("colorLanguage", ...
    ```
    This is required due to need to execute the code within a particular WebView instance hosting the Monaco control, there is no global execution context. Therefore, you must register any language providers individually to each `CodeEditor` instance.
  - Returns using `IAsyncOperation<T>` usually need to use the `AsyncInfo.Run` system interop helper. See Issue #45.
  - `Uri` class is not mapped yet to a built-in C# type. See Issue #33.
  - Keyboard Events can't use the built-in system `KeyRoutedEventArgs` class as it is sealed. (See https://github.com/microsoft/microsoft-ui-xaml/issues/5475)
  - Decorations use CSS class names in Monaco, this has been ported to be a strongly-typed abstraction per type of styled element required for use of decorations. Each style type has a set or properties using common WinRT types associated with styling UI elements like `SolidColorBrush`, `TextDecoration`, `FontStyle`, etc... If a property is missing that you require, please open an issue or PR to modify the corresponding `ICssStyle` implementations.


Build Notes
-----------
Requires the **.NET 10 SDK**.  The project targets `net10.0-browserwasm` and `net10.0-desktop`.

The vendored Monaco editor distribution is not included in this repository and can be downloaded from the [Monaco site](https://microsoft.github.io/monaco-editor/).  The contents of its uncompressed 'package' directory should be placed in the *MonacoEditorComponent/monaco-editor* directory.  The `install-dependencies.ps1` PowerShell script can install this for you automatically.

To build:
```bash
pwsh ./install-dependencies.ps1
dotnet restore MonacoEditorComponent.slnx
dotnet build MonacoEditorComponent.slnx --no-restore
```

Type Generation
---------------
The C# API surface in `MonacoEditorComponent/Monaco/` is generated from Monaco's TypeScript definitions using a two-stage pipeline:

1. **ts-morph extractor** (`tools/monaco-type-extractor/`): Parses `monaco.d.ts` into a versioned intermediate JSON model.
2. **.NET CLI emitter** (`tools/MonacoTypeEmitter/`): Emits C# classes with System.Text.Json attributes from the intermediate model.

To regenerate after updating the Monaco version:
```bash
npx tsx tools/monaco-type-extractor/src/index.ts -- node_modules/monaco-editor/monaco.d.ts -o tools/monaco-type-extractor/output/model.json
dotnet run --project tools/MonacoTypeEmitter -- --input tools/monaco-type-extractor/output/model.json --output MonacoEditorComponent/Monaco/
```

License
-------
MIT
