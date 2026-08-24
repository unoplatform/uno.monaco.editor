using System.Globalization;
using System.Text.Json;

using Monaco.Serialization;

namespace Monaco.Editor;

/// <summary>
/// Helper to access IModel interface methods off of CodeEditor object.
/// <see href="https://microsoft.github.io/monaco-editor/typedoc/types/editor_editor_api.editor.IModel.html">monaco.editor.IModel</see>,
/// <see href="https://microsoft.github.io/monaco-editor/typedoc/interfaces/editor_editor_api.editor.ITextModel.html">monaco.editor.ITextModel</see>
/// </summary>
public sealed class ModelHelper(CodeEditor editor) : IModel
{
    private readonly WeakReference<CodeEditor> _editor = new(editor);

    public string Id => throw new NotImplementedException();

    public Uri Uri => throw new NotImplementedException();

    public async Task DetectIndentationAsync(bool defaultInsertSpaces, bool defaultTabSize)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            await editor.InvokeScriptAsync("EditorContext.getEditorForElement(element).model.detectIndentationAsync", [defaultInsertSpaces, defaultTabSize]);
        }
    }

    public async Task<IEnumerable<FindMatch>> FindMatchesAsync(string searchString, bool searchOnlyEditableRange, bool isRegex, bool matchCase, string? wordSeparators, bool captureMatches)
    {
        // Default limit results: https://github.com/microsoft/vscode/blob/b2d0292a20c4a012005c94975019a5b572ce6a63/src/vs/editor/common/model/textModel.ts#L117
        return await FindMatchesAsync(searchString, searchOnlyEditableRange, isRegex, matchCase, wordSeparators, captureMatches, 999);
    }

    public async Task<IEnumerable<FindMatch>> FindMatchesAsync(string searchString, IRange searchScope, bool isRegex, bool matchCase, string? wordSeparators, bool captureMatches)
    {
        // Default limit results: https://github.com/microsoft/vscode/blob/b2d0292a20c4a012005c94975019a5b572ce6a63/src/vs/editor/common/model/textModel.ts#L117
        return await FindMatchesAsync(searchString, searchScope, isRegex, matchCase, wordSeparators, captureMatches, 999);
    }

    public async Task<IEnumerable<FindMatch>> FindMatchesAsync(string searchString, bool searchOnlyEditableRange, bool isRegex, bool matchCase, string? wordSeparators, bool captureMatches, double limitResultCount)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            var script = BuildFindMatchesScript(searchString, searchOnlyEditableRange, isRegex, matchCase, wordSeparators, captureMatches, limitResultCount);

            return await editor.SendScriptAsync<IEnumerable<FindMatch>>(script) ?? [];
        }

        return [];
    }

    public async Task<IEnumerable<FindMatch>> FindMatchesAsync(string searchString, IRange searchScope, bool isRegex, bool matchCase, string? wordSeparators, bool captureMatches, double limitResultCount)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            var script = BuildFindMatchesScript(searchString, searchScope, isRegex, matchCase, wordSeparators, captureMatches, limitResultCount);

            return await editor.SendScriptAsync<IEnumerable<FindMatch>>(script) ?? [];
        }

        return [];
    }

    public async Task<FindMatch?> FindNextMatchAsync(string searchString, IPosition searchStart, bool isRegex, bool matchCase, string? wordSeparators, bool captureMatches)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            var script = BuildFindMatchScript("findNextMatch", searchString, searchStart, isRegex, matchCase, wordSeparators, captureMatches);

            return await editor.SendScriptAsync<FindMatch>(script);
        }

        return null;
    }

    public async Task<FindMatch?> FindPreviousMatchAsync(string searchString, IPosition searchStart, bool isRegex, bool matchCase, string wordSeparators, bool captureMatches)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            var script = BuildFindMatchScript("findPreviousMatch", searchString, searchStart, isRegex, matchCase, wordSeparators, captureMatches);

            return await editor.SendScriptAsync<FindMatch>(script);
        }

        return null;
    }

    public async Task<uint> GetAlternativeVersionIdAsync()
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getAlternativeVersionId();").AsAsyncOperation();
        }

        return 0;
    }

    public async Task<string?> GetEOLAsync()
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.getEOL();").AsAsyncOperation();
        }

        return null;
    }

    public async Task<Range?> GetFullModelRangeAsync()
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<Range>("EditorContext.getEditorForElement(element).model.getFullModelRange();").AsAsyncOperation();
        }

        return null;
    }

    public async Task<string?> GetLineContentAsync(uint lineNumber)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.getLineContent(" + lineNumber + ");").AsAsyncOperation();
        }

        return null;
    }

    public async Task<uint> GetLineCountAsync()
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getLineCount();").AsAsyncOperation();
        }

        return 0;
    }

    public async Task<uint> GetLineFirstNonWhitespaceColumnAsync(uint lineNumber)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getLineFirstNonWhitespaceColumn(" + lineNumber + ");").AsAsyncOperation();
        }

        return 0;
    }

    public async Task<uint> GetLineLastNonWhitespaceColumnAsync(uint lineNumber)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getLineLastNonWhitespaceColumn(" + lineNumber + ");").AsAsyncOperation();
        }

        return 0;
    }

    public async Task<uint> GetLineLengthAsync(uint lineNumber)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getLineLength(" + lineNumber + ");").AsAsyncOperation();
        }

        return 0;
    }

    public async Task<uint> GetLineMaxColumnAsync(uint lineNumber)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getLineMaxColumn(" + lineNumber + ");").AsAsyncOperation();
        }

        return 0;
    }

    public async Task<uint> GetLineMinColumnAsync(uint lineNumber)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getLineMinColumn(" + lineNumber + ");").AsAsyncOperation();
        }

        return 0;
    }

    public async Task<IEnumerable<string>> GetLinesContentAsync()
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<IEnumerable<string>>("EditorContext.getEditorForElement(element).model.getLinesContent();").AsAsyncOperation();
        }

        return [];
    }

    public async Task<string?> GetModelIdAsync()
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.getModelId();").AsAsyncOperation();
        }

        return null;
    }

    public async Task<uint> GetOffsetAtAsync(IPosition position)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getOffsetAt(" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + ");").AsAsyncOperation();
        }

        return 0;
    }

    public async Task<string?> GetOneIndentAsync()
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.getOneIndent();").AsAsyncOperation();
        }

        return null;
    }

    public async Task<Position?> GetPositionAtAsync(uint offset)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<Position>("EditorContext.getEditorForElement(element).model.getPositionAt(" + offset + ");").AsAsyncOperation();
        }

        return null;
    }

    public async Task<string?> GetValueAsync()
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.getValue();").AsAsyncOperation();
        }

        return null;
    }

    public async Task<string?> GetValueAsync(EndOfLinePreference eol)
    {
        throw new NotImplementedException();
    }

    public async Task<string?> GetValueAsync(EndOfLinePreference eol, bool preserveBOM)
    {
        throw new NotImplementedException();
    }

    public async Task<string?> GetValueInRangeAsync(IRange range)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.getValueInRange(" + JsonSerializer.Serialize(Range.Lift(range), MonacoJsonContext.Relaxed.Range) + ");").AsAsyncOperation();
        }

        return null;
    }

    public async Task<string?> GetValueInRangeAsync(IRange range, EndOfLinePreference eol)
    {
        throw new NotImplementedException();
    }

    public async Task<uint> GetValueLengthAsync()
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getValueLength();").AsAsyncOperation();
        }

        return 0;
    }

    public async Task<uint> GetValueLengthAsync(EndOfLinePreference eol)
    {
        throw new NotImplementedException();
    }

    public async Task<uint> GetValueLengthAsync(EndOfLinePreference eol, bool preserveBOM)
    {
        throw new NotImplementedException();
    }

    public async Task<uint> GetValueLengthInRangeAsync(IRange range)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getValueLengthInRange(" + JsonSerializer.Serialize(Range.Lift(range), MonacoJsonContext.Relaxed.Range) + ");").AsAsyncOperation();
        }

        return 0;
    }

    public async Task<uint> GetVersionIdAsync()
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getVersionId();").AsAsyncOperation();
        }

        return 0;
    }

    // TODO: Need to investigate why with .NET Native the InterfaceToClassConverter isn't working anymore?
    public async Task<WordAtPosition?> GetWordAtPositionAsync(IPosition position)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<WordAtPosition>("EditorContext.getEditorForElement(element).model.getWordAtPosition(" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + ");").AsAsyncOperation();
        }

        return null;
    }

    public async Task<WordAtPosition?> GetWordUntilPositionAsync(IPosition position)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<WordAtPosition>("EditorContext.getEditorForElement(element).model.getWordUntilPosition(" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + ");").AsAsyncOperation();
        }

        return null;
    }

    public async Task<Position?> ModifyPositionAsync(IPosition position, int number)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<Position>("EditorContext.getEditorForElement(element).model.modifyPosition(" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + ", " + number + ");").AsAsyncOperation();
        }

        return null;
    }

    public async Task<string?> NormalizeIndentationAsync(string str)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.normalizeIndentations(JSON.parse(" + JsonSerializer.Serialize(str, MonacoJsonContext.Relaxed.Options) + "));").AsAsyncOperation();
        }

        return null;
    }

    public async Task PushStackElementAsync()
    {
        if (_editor.TryGetTarget(out var editor))
        {
            await editor.SendScriptAsync("EditorContext.getEditorForElement(element).model.pushStackElement();");
        }
    }

    public async Task SetEOLAsync(EndOfLineSequence eol)
    {
        throw new NotImplementedException();
    }

    public async Task SetValue(string newValue)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            await editor.SendScriptAsync("EditorContext.getEditorForElement(element).model.setValue(JSON.parse(" + JsonSerializer.Serialize(newValue, MonacoJsonContext.Relaxed.Options) + "));");
        }
    }

    public async Task<Position?> ValidatePositionAsync(IPosition position)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<Position>("EditorContext.getEditorForElement(element).model.validatePosition(" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + ");").AsAsyncOperation();
        }

        return null;
    }

    public async Task<Range?> ValidateRangeAsync(IRange range)
    {
        if (_editor.TryGetTarget(out var editor))
        {
            return await editor.SendScriptAsync<Range>("EditorContext.getEditorForElement(element).model.validateRange(" + JsonSerializer.Serialize(Range.Lift(range), MonacoJsonContext.Relaxed.Range) + ");").AsAsyncOperation();
        }

        return null;
    }

    /// <summary>
    /// Builds a <c>model.findMatches(...)</c> call.
    /// </summary>
    /// <remarks>
    /// These scripts are sent with <c>SendScriptAsync</c>, not <c>InvokeScriptAsync</c>: the latter
    /// emits <c>method(element, ...args)</c> so the host can resolve the editor element, which shifts
    /// every argument by one when the target is a Monaco API rather than one of our helper functions.
    /// Monaco then read the DOM element as the search pattern (<c>[object HTMLDivElement]</c>, a
    /// character class that matches an isolated space) and <c>true</c> as the result limit, so every
    /// search reported one bogus match instead of none.
    /// </remarks>
    internal static string BuildFindMatchesScript(string searchString, bool searchOnlyEditableRange, bool isRegex, bool matchCase, string? wordSeparators, bool captureMatches, double limitResultCount)
        => BuildFindMatchesScript(searchString, JsArg(searchOnlyEditableRange), isRegex, matchCase, wordSeparators, captureMatches, limitResultCount);

    /// <inheritdoc cref="BuildFindMatchesScript(string, bool, bool, bool, string?, bool, double)"/>
    internal static string BuildFindMatchesScript(string searchString, IRange searchScope, bool isRegex, bool matchCase, string? wordSeparators, bool captureMatches, double limitResultCount)
        => BuildFindMatchesScript(searchString, JsArg(searchScope), isRegex, matchCase, wordSeparators, captureMatches, limitResultCount);

    private static string BuildFindMatchesScript(string searchString, string searchScope, bool isRegex, bool matchCase, string? wordSeparators, bool captureMatches, double limitResultCount)
        => "EditorContext.getEditorForElement(element).model.findMatches("
            + JsArg(searchString) + ", "
            + searchScope + ", "
            + JsArg(isRegex) + ", "
            + JsArg(matchCase) + ", "
            + JsArg(wordSeparators) + ", "
            + JsArg(captureMatches) + ", "
            + JsArg(limitResultCount) + ");";

    /// <summary>
    /// Builds a <c>model.findNextMatch(...)</c> / <c>model.findPreviousMatch(...)</c> call.
    /// </summary>
    /// <remarks>See <see cref="BuildFindMatchesScript(string, bool, bool, bool, string?, bool, double)"/> for why these are not invoked as methods.</remarks>
    internal static string BuildFindMatchScript(string method, string searchString, IPosition searchStart, bool isRegex, bool matchCase, string? wordSeparators, bool captureMatches)
        => "EditorContext.getEditorForElement(element).model." + method + "("
            + JsArg(searchString) + ", "
            + JsArg(searchStart) + ", "
            + JsArg(isRegex) + ", "
            + JsArg(matchCase) + ", "
            + JsArg(wordSeparators) + ", "
            + JsArg(captureMatches) + ");";

    private static string JsArg(string? value)
        => value is null ? "null" : JsonSerializer.Serialize(value, MonacoJsonContext.Relaxed.Options);

    private static string JsArg(bool value) => value ? "true" : "false";

    private static string JsArg(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string JsArg(IRange range) => JsonSerializer.Serialize(Range.Lift(range), MonacoJsonContext.Relaxed.Range);

    private static string JsArg(IPosition position) => JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position);
}
