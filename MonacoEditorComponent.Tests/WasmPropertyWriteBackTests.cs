using System.Text.Json;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Fidelity of the editor-to-host write-back on WASM: the document and selection Monaco reports
/// through the JSExport bridge must land on the control's properties exactly as Monaco holds them.
/// </summary>
/// <remarks>
/// <para>
/// The content and selection listeners in <c>asyncCallbackHelpers.ts</c> send the model's raw
/// text through <c>ManagedSetValue</c>. That boundary used to treat the text as if it were a JSON
/// string literal -- trimming quotation marks, halving doubled backslashes, and turning a literal
/// <c>\t</c> or <c>\r\n</c> into control characters -- which went unnoticed while the write was
/// being dropped on the presenter. These tests drive the real app through Monaco's API, then read
/// the managed property back through <c>getParentJsonValue</c>, the same JSExport read path the
/// control uses, and compare it with what Monaco itself reports.
/// </para>
/// <para>
/// Expected values are read from Monaco <i>after</i> the write rather than taken from the input,
/// so the only normalization tolerated is Monaco's own end-of-line handling on <c>setValue</c>.
/// </para>
/// </remarks>
[Trait("Category", "WasmPlaywright")]
[Collection("WasmPlaywright")]
public sealed class WasmPropertyWriteBackTests : IAsyncLifetime
{
    /// <summary>
    /// The write-back is synchronous on WASM's single thread, so this only has to absorb the
    /// dispatcher hop taken when the bridge is entered without thread access.
    /// </summary>
    private const int WriteBackTimeoutMs = 5_000;

    /// <summary>Target token for the sample's plain <c>CodeEditor</c>.</summary>
    private const string PlainEditor = "plain";

    /// <summary>Target token for the modified pane of the sample's <c>DiffCodeEditor</c>.</summary>
    private const string ModifiedEditor = "modified";

    /// <summary>
    /// A line holding a quoted string literal with escaped quotes inside it.
    /// Columns 9-20 (one-based, inclusive) are the literal including its delimiters.
    /// </summary>
    private const string QuotedLine = """var s = "say \"hi\"";""";

    /// <summary>
    /// A line holding a Windows path with doubled backslashes that ends in backslash-backslash-quote,
    /// the run the JSON-path decoder collapses. Columns 9-25 are the literal including its delimiters.
    /// </summary>
    private const string PathLine = """var p = "C:\\Users\\me\\";""";

    private readonly WasmAppFixture _fixture;
    private string _currentTestName = "unknown";
    private bool _testFailed;

    public WasmPropertyWriteBackTests(WasmAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _testFailed = false;
        await _fixture.ResetEditorStateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_testFailed)
        {
            await _fixture.CaptureFailureArtifacts(_currentTestName);
        }
    }

    /// <summary>
    /// Documents the JSExport boundary used to alter. Each is source a user could plausibly have
    /// in the editor: a quoted string, an empty string literal, escape sequences written out in
    /// code, Windows paths, whitespace at the edges, real control characters, and every character
    /// the transport encoding touches.
    /// </summary>
    public static TheoryData<string> Documents =>
    [
        "\"hello\"",
        "\"\"",
        QuotedLine,
        "a\\tb",
        "line1\\r\\nline2",
        "C:\\\\Users\\\\me",
        PathLine,
        "  padded  ",
        "\tindented\nsecond line\n",
        "100% of {items}: 'a', \"b\" & c",
    ];

    [Theory]
    [Trait("Category", "WasmPlaywright")]
    [MemberData(nameof(Documents))]
    public async Task Text_MatchesTheDocumentMonacoDisplays(string document)
    {
        _currentTestName = nameof(Text_MatchesTheDocumentMonacoDisplays);
        try
        {
            var displayed = await SetDocumentAsync(PlainEditor, document);

            var managed = await WaitForManagedStringAsync(PlainEditor, "Text", displayed);

            Assert.Equal(displayed, managed);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// The diff control's editable side reports through the same listener under the name
    /// <c>ModifiedText</c>, so it is exercised separately rather than assumed from <c>Text</c>.
    /// </summary>
    [Theory]
    [Trait("Category", "WasmPlaywright")]
    [MemberData(nameof(Documents))]
    public async Task ModifiedText_MatchesTheDocumentMonacoDisplays(string document)
    {
        _currentTestName = nameof(ModifiedText_MatchesTheDocumentMonacoDisplays);

        await _fixture.Page.WaitForFunctionAsync(
            DiffEditorCases.IsDiffEditorPresentExpression,
            null, new() { Timeout = WriteBackTimeoutMs });

        var before = await _fixture.Page.EvaluateAsync<string>(DiffEditorCases.ModifiedValueExpression);
        try
        {
            var displayed = await SetDocumentAsync(ModifiedEditor, document);

            var managed = await WaitForManagedStringAsync(ModifiedEditor, "ModifiedText", displayed);

            Assert.Equal(displayed, managed);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
        finally
        {
            // The fixture's reset only covers the plain editor; leave the diff sample as the
            // other tests in this collection expect to find it.
            await SetDocumentAsync(ModifiedEditor, before);
        }
    }

    /// <summary>
    /// <c>SelectedText</c> travels the raw-text path and <c>SelectedRange</c> the typed JSON
    /// path, from the same selection event. Both must agree with Monaco, including when the
    /// selection is exactly a quoted literal or ends in backslash-backslash-quote.
    /// </summary>
    [Fact]
    [Trait("Category", "WasmPlaywright")]
    public async Task Selection_ReportsSelectedTextAndRangeExactly()
    {
        _currentTestName = nameof(Selection_ReportsSelectedTextAndRangeExactly);
        try
        {
            var displayed = await SetDocumentAsync(PlainEditor, QuotedLine + "\n" + PathLine);
            Assert.Equal(QuotedLine + "\n" + PathLine, displayed);

            // (range, the text Monaco must report for it)
            (EditorRange Range, string Expected)[] cases =
            [
                (new(1, 9, 1, 21), QuotedLine[8..20]),
                (new(2, 9, 2, 26), PathLine[8..25]),
                (new(1, 1, 2, PathLine.Length + 1), displayed),
            ];

            foreach (var (range, expected) in cases)
            {
                var selectedInMonaco = await SelectAsync(PlainEditor, range);
                Assert.Equal(expected, selectedInMonaco);

                var managedText = await WaitForManagedStringAsync(PlainEditor, "SelectedText", selectedInMonaco);
                Assert.Equal(selectedInMonaco, managedText);

                var managedRange = Assert.NotNull(await ReadManagedAsync(PlainEditor, "SelectedRange"));
                Assert.Equal(JsonValueKind.Object, managedRange.ValueKind);
                Assert.Equal(range.StartLineNumber, managedRange.GetProperty("startLineNumber").GetInt32());
                Assert.Equal(range.StartColumn, managedRange.GetProperty("startColumn").GetInt32());
                Assert.Equal(range.EndLineNumber, managedRange.GetProperty("endLineNumber").GetInt32());
                Assert.Equal(range.EndColumn, managedRange.GetProperty("endColumn").GetInt32());
            }
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>A Monaco <c>IRange</c>, one-based and end-exclusive on the column.</summary>
    private sealed record EditorRange(int StartLineNumber, int StartColumn, int EndLineNumber, int EndColumn);

    /// <summary>JS statement resolving <c>editor</c> from the <c>target</c> token in scope.</summary>
    private static readonly string ResolveEditorJs = $$"""
        const editor = target === '{{ModifiedEditor}}'
            ? {{DiffEditorCases.StandaloneDiffEditorsExpressionBody}}[0].getModifiedEditor()
            : {{DiffEditorCases.StandaloneEditorsExpressionBody}}[0];
        """;

    /// <summary>
    /// Writes the document into the target editor through Monaco and returns the text Monaco
    /// holds afterwards -- the oracle the managed property is compared against.
    /// </summary>
    private static readonly string SetDocumentExpression = $$"""
        ({ target, text }) => {
            {{ResolveEditorJs}}
            editor.setValue(text);
            return editor.getValue();
        }
        """;

    /// <summary>Selects the range in the target editor and returns the text Monaco reports for it.</summary>
    private static readonly string SelectExpression = $$"""
        ({ target, range }) => {
            {{ResolveEditorJs}}
            editor.setSelection(range);
            return editor.getModel().getValueInRange(editor.getSelection());
        }
        """;

    /// <summary>
    /// Reads a property of the control that owns the target editor through <c>getParentJsonValue</c>,
    /// the JSExport read path the control itself uses, decoded to a JS value.
    /// </summary>
    /// <remarks>
    /// The control is found through <c>EditorContext</c>, keyed by host element; a diff context
    /// aliases its modified sub-editor onto <c>editor</c>, so identity on that field selects the
    /// right owner for both targets.
    /// </remarks>
    private static readonly string ReadManagedExpression = $$"""
        ({ target, name }) => {
            {{ResolveEditorJs}}
            const entry = [...EditorContext._editors.entries()].find(([, context]) => context.editor === editor);
            if (!entry) { throw new Error(`No EditorContext owns the ${target} editor`); }
            return JSON.parse(desanitize(getParentJsonValue(entry[0], name)));
        }
        """;

    private Task<string> SetDocumentAsync(string target, string text) =>
        _fixture.Page.EvaluateAsync<string>(SetDocumentExpression, new Dictionary<string, object>
        {
            ["target"] = target,
            ["text"] = text,
        });

    private Task<string> SelectAsync(string target, EditorRange range) =>
        _fixture.Page.EvaluateAsync<string>(SelectExpression, new Dictionary<string, object>
        {
            ["target"] = target,
            ["range"] = new Dictionary<string, object>
            {
                ["startLineNumber"] = range.StartLineNumber,
                ["startColumn"] = range.StartColumn,
                ["endLineNumber"] = range.EndLineNumber,
                ["endColumn"] = range.EndColumn,
            },
        });

    private Task<JsonElement?> ReadManagedAsync(string target, string name) =>
        _fixture.Page.EvaluateAsync<JsonElement?>(ReadManagedExpression, new Dictionary<string, object>
        {
            ["target"] = target,
            ["name"] = name,
        });

    /// <summary>
    /// Polls the managed string property until it equals <paramref name="expected"/> or the
    /// timeout elapses, returning the last value observed so the caller's assertion shows the
    /// actual text on failure instead of a bare timeout.
    /// </summary>
    private async Task<string?> WaitForManagedStringAsync(string target, string name, string expected)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(WriteBackTimeoutMs);
        string? observed;

        while (true)
        {
            var value = await ReadManagedAsync(target, name);
            observed = value is { ValueKind: JsonValueKind.String } text ? text.GetString() : value?.ToString();

            if (observed == expected || DateTime.UtcNow >= deadline)
            {
                return observed;
            }

            await Task.Delay(50);
        }
    }
}
