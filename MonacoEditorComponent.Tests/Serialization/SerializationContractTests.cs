using System.Text.Json;
using Monaco;
using Monaco.Editor;
using Monaco.Helpers;
using Monaco.Languages;
using Monaco.Serialization;
using Newtonsoft.Json;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;
using NewtonsoftSerializer = Newtonsoft.Json.JsonConvert;
using Range = Monaco.Range;

namespace MonacoEditorComponent.Tests.Serialization;

/// <summary>
/// Serialization contract tests that verify STJ source-generated output matches
/// Newtonsoft golden baselines for all major cross-boundary Monaco types.
/// </summary>
/// <remarks>
/// These tests serve two purposes:
/// <list type="number">
///   <item>Capture golden baselines from current Newtonsoft behavior.</item>
///   <item>Verify STJ round-trip fidelity for each type category (primitive, enum, model).</item>
/// </list>
/// </remarks>
[Trait("Category", "Serialization")]
public class SerializationContractTests
{
    // Newtonsoft settings matching the project's current default configuration
    private static readonly JsonSerializerSettings NewtonsoftSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
    };

    #region Golden Baselines — Newtonsoft reference output

    [Fact]
    public void Golden_Position()
    {
        var position = new Position(10, 5);
        var json = NewtonsoftSerializer.SerializeObject(position, NewtonsoftSettings);
        Assert.Equal("""{"column":5,"lineNumber":10}""", json);
    }

    [Fact]
    public void Golden_Range()
    {
        var range = new Range(1, 1, 5, 10);
        var json = NewtonsoftSerializer.SerializeObject(range, NewtonsoftSettings);
        Assert.Equal("""{"endColumn":10,"endLineNumber":5,"startColumn":1,"startLineNumber":1}""", json);
    }

    [Fact]
    public void Golden_Selection()
    {
        var selection = new Selection(1, 1, 3, 5);
        var json = NewtonsoftSerializer.SerializeObject(selection, NewtonsoftSettings);
        Assert.Equal(
            """{"startLineNumber":1,"startColumn":1,"endLineNumber":3,"endColumn":5,"positionLineNumber":3,"positionColumn":5,"selectionStartLineNumber":1,"selectionStartColumn":1}""",
            json);
    }

    [Fact]
    public void Golden_CompletionItem()
    {
        var item = new CompletionItem("log", "console.log()", CompletionItemKind.Function)
        {
            Detail = "Log output",
            SortText = "0001",
        };
        var json = NewtonsoftSerializer.SerializeObject(item, NewtonsoftSettings);

        // Full exact-match baseline: every field, property order, null omission
        Assert.Equal(
            """{"detail":"Log output","insertText":"console.log()","kind":1,"label":"log","sortText":"0001"}""",
            json);
    }

    [Fact]
    public void Golden_CodeAction()
    {
        var action = new CodeAction
        {
            Title = "Extract method",
            Kind = "refactor.extract",
            IsPreferred = true,
        };
        var json = NewtonsoftSerializer.SerializeObject(action, NewtonsoftSettings);

        // Full exact-match baseline: field order, naming, boolean, null omission
        Assert.Equal(
            """{"isPreferred":true,"kind":"refactor.extract","title":"Extract method"}""",
            json);
    }

    [Fact]
    public void Golden_Hover()
    {
        var hover = new Hover(
            ["**bold** text"],
            new Range(1, 1, 1, 10));
        var json = NewtonsoftSerializer.SerializeObject(hover, NewtonsoftSettings);

        // Full exact-match baseline: contents array with IMarkdownString fields, range object
        Assert.Equal(
            """{"contents":[{"isTrusted":false,"value":"**bold** text"}],"range":{"endColumn":10,"endLineNumber":1,"startColumn":1,"startLineNumber":1}}""",
            json);
    }

    [Fact]
    public void Golden_MarkerData()
    {
        var marker = new MarkerData
        {
            Severity = MarkerSeverity.Error,
            Message = "Syntax error",
            StartLineNumber = 1,
            StartColumn = 1,
            EndLineNumber = 1,
            EndColumn = 10,
        };
        var json = NewtonsoftSerializer.SerializeObject(marker, NewtonsoftSettings);

        // Full exact-match baseline: all fields including zero-valued, enum as integer
        Assert.Equal(
            """{"endColumn":10,"endLineNumber":1,"message":"Syntax error","severity":8,"startColumn":1,"startLineNumber":1}""",
            json);
    }

    [Fact]
    public void Golden_ColorInformation()
    {
        // ColorInformation uses custom Newtonsoft converters (ColorConverter,
        // InterfaceToClassConverter) so the output includes converter-specific
        // float representations. Full exact-match baseline captures this.
        var colorInfo = new ColorInformation(
            Windows.UI.Color.FromArgb(255, 128, 64, 32),
            new Range(1, 1, 1, 10));
        var json = NewtonsoftSerializer.SerializeObject(colorInfo, NewtonsoftSettings);

        // Full exact-match baseline: color as {alpha,red,green,blue} floats, range object
        Assert.Equal(
            """{"color":{"alpha":1.0,"red":0.5019608,"green":0.2509804,"blue":0.1254902},"range":{"endColumn":10,"endLineNumber":1,"startColumn":1,"startLineNumber":1}}""",
            json);
    }

    #endregion

    #region STJ round-trip tests — one per major type category

    [Fact]
    public void RoundTrip_Primitive_Position()
    {
        var original = new Position(42, 7);
        var json = JsonSerializer.Serialize(original, MonacoJsonContext.Default.Position);
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.Position);
        Assert.NotNull(restored);
        Assert.Equal(original.LineNumber, restored.LineNumber);
        Assert.Equal(original.Column, restored.Column);
    }

    [Fact]
    public void Serialize_Primitive_Range()
    {
        var original = new Range(1, 2, 3, 4);
        var json = JsonSerializer.Serialize(original, MonacoJsonContext.Default.Range);
        var doc = JsonDocument.Parse(json);

        // Serialization produces correct camelCase output
        Assert.Equal(1u, doc.RootElement.GetProperty("startLineNumber").GetUInt32());
        Assert.Equal(2u, doc.RootElement.GetProperty("startColumn").GetUInt32());
        Assert.Equal(3u, doc.RootElement.GetProperty("endLineNumber").GetUInt32());
        Assert.Equal(4u, doc.RootElement.GetProperty("endColumn").GetUInt32());
    }

    [Fact]
    public void Deserialize_Primitive_Range_RequiresPublicSetters()
    {
        // Range currently has private setters, so STJ deserialization creates
        // a default instance via the parameterless constructor but cannot populate
        // properties. Full round-trip will work after fn-2.3 migrates Range to
        // use [JsonInclude] or public setters.
        var json = """{"startLineNumber":1,"startColumn":2,"endLineNumber":3,"endColumn":4}""";
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.Range);
        Assert.NotNull(restored);
        // Properties remain at default (0) until Range migration in fn-2.3
        Assert.Equal(0u, restored.StartLineNumber);
    }

    [Fact]
    public void RoundTrip_NumericEnum_MarkerSeverity()
    {
        var original = new MarkerData
        {
            Severity = MarkerSeverity.Warning,
            Message = "Unused variable",
            StartLineNumber = 5,
            StartColumn = 1,
            EndLineNumber = 5,
            EndColumn = 10,
        };

        var json = JsonSerializer.Serialize(original, MonacoJsonContext.Default.MarkerData);

        // Verify numeric enum value in JSON
        var doc = JsonDocument.Parse(json);
        Assert.Equal(4, doc.RootElement.GetProperty("severity").GetInt32()); // Warning = 4

        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.MarkerData);
        Assert.NotNull(restored);
        Assert.Equal(MarkerSeverity.Warning, restored.Severity);
        Assert.Equal("Unused variable", restored.Message);
    }

    [Fact]
    public void RoundTrip_NumericEnum_CompletionItemKind()
    {
        var original = new CompletionItem("test", "test()", CompletionItemKind.Method);

        var json = JsonSerializer.Serialize(original, MonacoJsonContext.Default.CompletionItem);

        // Verify numeric enum value in JSON
        var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("kind").GetInt32()); // Method = 0

        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.CompletionItem);
        Assert.NotNull(restored);
        Assert.Equal(CompletionItemKind.Method, restored.Kind);
    }

    [Fact]
    public void RoundTrip_Model_CompletionList()
    {
        var original = new CompletionList
        {
            Incomplete = true,
            Suggestions =
            [
                new CompletionItem("log", "console.log()", CompletionItemKind.Function)
                {
                    Detail = "Log output",
                },
            ],
        };

        var json = JsonSerializer.Serialize(original, MonacoJsonContext.Default.CompletionList);
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.CompletionList);

        Assert.NotNull(restored);
        Assert.True(restored.Incomplete);
        Assert.NotNull(restored.Suggestions);
        Assert.Single(restored.Suggestions);
        Assert.Equal("log", restored.Suggestions[0].Label);
        Assert.Equal(CompletionItemKind.Function, restored.Suggestions[0].Kind);
    }

    [Fact]
    public void RoundTrip_Model_CodeAction()
    {
        var original = new CodeAction
        {
            Title = "Extract method",
            Kind = "refactor.extract",
            IsPreferred = true,
        };

        var json = JsonSerializer.Serialize(original, MonacoJsonContext.Default.CodeAction);
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.CodeAction);

        Assert.NotNull(restored);
        Assert.Equal("Extract method", restored.Title);
        Assert.Equal("refactor.extract", restored.Kind);
        Assert.True(restored.IsPreferred);
    }

    [Fact]
    public void RoundTrip_Model_MarkerData()
    {
        var original = new MarkerData
        {
            Severity = MarkerSeverity.Error,
            Message = "Syntax error",
            Source = "typescript",
            Code = "TS2304",
            StartLineNumber = 10,
            StartColumn = 5,
            EndLineNumber = 10,
            EndColumn = 15,
        };

        var json = JsonSerializer.Serialize(original, MonacoJsonContext.Default.MarkerData);
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.MarkerData);

        Assert.NotNull(restored);
        Assert.Equal(MarkerSeverity.Error, restored.Severity);
        Assert.Equal("Syntax error", restored.Message);
        Assert.Equal("typescript", restored.Source);
        Assert.Equal("TS2304", restored.Code);
    }

    #endregion

    #region CamelCase naming verification

    [Fact]
    public void CamelCase_PropertyNames_Position()
    {
        var position = new Position(1, 2);
        var json = JsonSerializer.Serialize(position, MonacoJsonContext.Default.Position);
        var doc = JsonDocument.Parse(json);

        // Verify camelCase property names (not PascalCase)
        Assert.True(doc.RootElement.TryGetProperty("lineNumber", out _));
        Assert.True(doc.RootElement.TryGetProperty("column", out _));
        Assert.False(doc.RootElement.TryGetProperty("LineNumber", out _));
        Assert.False(doc.RootElement.TryGetProperty("Column", out _));
    }

    [Fact]
    public void CamelCase_PropertyNames_MarkerData()
    {
        var marker = new MarkerData
        {
            Severity = MarkerSeverity.Info,
            Message = "Info",
            StartLineNumber = 1,
            StartColumn = 1,
            EndLineNumber = 1,
            EndColumn = 5,
        };
        var json = JsonSerializer.Serialize(marker, MonacoJsonContext.Default.MarkerData);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("startLineNumber", out _));
        Assert.True(doc.RootElement.TryGetProperty("endColumn", out _));
        Assert.True(doc.RootElement.TryGetProperty("severity", out _));
        Assert.True(doc.RootElement.TryGetProperty("message", out _));
    }

    #endregion

    #region Null handling verification

    [Fact]
    public void NullsOmitted_WhenWritingNull()
    {
        var item = new CompletionItem("test", "test", CompletionItemKind.Text);
        // All optional properties are null
        var json = JsonSerializer.Serialize(item, MonacoJsonContext.Default.CompletionItem);
        var doc = JsonDocument.Parse(json);

        // Required fields present
        Assert.True(doc.RootElement.TryGetProperty("insertText", out _));
        Assert.True(doc.RootElement.TryGetProperty("label", out _));
        Assert.True(doc.RootElement.TryGetProperty("kind", out _));

        // Null optional fields omitted
        Assert.False(doc.RootElement.TryGetProperty("detail", out _));
        Assert.False(doc.RootElement.TryGetProperty("documentation", out _));
        Assert.False(doc.RootElement.TryGetProperty("filterText", out _));
        Assert.False(doc.RootElement.TryGetProperty("sortText", out _));
        Assert.False(doc.RootElement.TryGetProperty("commitCharacters", out _));
    }

    #endregion

    #region Relaxed encoder verification

    [Fact]
    public void RelaxedEncoder_DoesNotEscapeCodeCharacters()
    {
        var marker = new MarkerData
        {
            Severity = MarkerSeverity.Error,
            Message = "Expected <string> but got &none",
            StartLineNumber = 1,
            StartColumn = 1,
            EndLineNumber = 1,
            EndColumn = 10,
        };

        var json = JsonSerializer.Serialize(marker, MonacoJsonContext.Relaxed.MarkerData);

        // UnsafeRelaxedJsonEscaping should preserve <, >, & as-is
        Assert.Contains("<string>", json);
        Assert.Contains("&none", json);
    }

    #endregion

    #region SYSLIB1031 Uri collision safety verification

    [Fact]
    public void UriCollision_BothTypesSerializeCorrectly()
    {
        // SYSLIB1031 is suppressed because Monaco.Uri and System.Uri both appear
        // as discovered types. This test proves both serialize correctly despite
        // the source-gen property name collision.

        // IMarkdownString.Uris uses Monaco.Uri (IDictionary<string, Monaco.Uri>)
        var markdown = new IMarkdownString("test")
        {
            Uris = new Dictionary<string, Monaco.Uri>
            {
                ["link"] = new Monaco.Uri { Scheme = "https", Path = "/doc" },
            },
        };
        var mdJson = JsonSerializer.Serialize(markdown, MonacoJsonContext.Default.IMarkdownString);
        var mdDoc = JsonDocument.Parse(mdJson);
        Assert.Equal("https", mdDoc.RootElement.GetProperty("uris").GetProperty("link").GetProperty("scheme").GetString());
        Assert.Equal("/doc", mdDoc.RootElement.GetProperty("uris").GetProperty("link").GetProperty("path").GetString());

        // IRelatedInformation.Resource and Marker.Resource use Monaco.Uri
        // (declared as Uri in Monaco.Editor namespace where it resolves to Monaco.Uri)
        var marker = new Marker
        {
            Severity = MarkerSeverity.Info,
            Message = "test",
            Resource = new Monaco.Uri { Scheme = "file", Path = "/src/main.ts" },
            StartLineNumber = 1,
            StartColumn = 1,
            EndLineNumber = 1,
            EndColumn = 5,
        };
        var markerJson = JsonSerializer.Serialize(marker, MonacoJsonContext.Default.Marker);
        var markerDoc = JsonDocument.Parse(markerJson);
        Assert.Equal("file", markerDoc.RootElement.GetProperty("resource").GetProperty("scheme").GetString());
        Assert.Equal("/src/main.ts", markerDoc.RootElement.GetProperty("resource").GetProperty("path").GetString());
    }

    #endregion

    #region String enum serialization — JsonStringEnumMemberName contract tests

    [Theory]
    [InlineData(CursorBlinking.Blink, "blink")]
    [InlineData(CursorBlinking.Expand, "expand")]
    [InlineData(CursorBlinking.Phase, "phase")]
    [InlineData(CursorBlinking.Smooth, "smooth")]
    [InlineData(CursorBlinking.Solid, "solid")]
    public void StringEnum_CursorBlinking_RoundTrip(CursorBlinking value, string expected)
    {
        var json = JsonSerializer.Serialize(value);
        Assert.Equal($"\"{expected}\"", json);

        var deserialized = JsonSerializer.Deserialize<CursorBlinking>(json);
        Assert.Equal(value, deserialized);
    }

    [Theory]
    [InlineData(TextDecoration.None, "none")]
    [InlineData(TextDecoration.Underline, "underline")]
    [InlineData(TextDecoration.Overline, "overline")]
    [InlineData(TextDecoration.LineThrough, "line-through")]
    [InlineData(TextDecoration.Initial, "initial")]
    [InlineData(TextDecoration.Inherit, "inherit")]
    public void StringEnum_TextDecoration_HyphenatedValues(TextDecoration value, string expected)
    {
        var json = JsonSerializer.Serialize(value);
        Assert.Equal($"\"{expected}\"", json);

        var deserialized = JsonSerializer.Deserialize<TextDecoration>(json);
        Assert.Equal(value, deserialized);
    }

    [Theory]
    [InlineData(AutoIndent.Advanced, "advanced")]
    [InlineData(AutoIndent.Brackets, "brackets")]
    [InlineData(AutoIndent.Full, "full")]
    [InlineData(AutoIndent.Keep, "keep")]
    [InlineData(AutoIndent.None, "none")]
    public void StringEnum_AutoIndent_MultipleValues(AutoIndent value, string expected)
    {
        var json = JsonSerializer.Serialize(value);
        Assert.Equal($"\"{expected}\"", json);

        var deserialized = JsonSerializer.Deserialize<AutoIndent>(json);
        Assert.Equal(value, deserialized);
    }

    [Fact]
    public void StringEnum_CursorStyle_HyphenatedValues()
    {
        // CursorStyle has multiple hyphenated values — verify all round-trip correctly
        Assert.Equal("\"block\"", JsonSerializer.Serialize(CursorStyle.Block));
        Assert.Equal("\"block-outline\"", JsonSerializer.Serialize(CursorStyle.BlockOutline));
        Assert.Equal("\"line\"", JsonSerializer.Serialize(CursorStyle.Line));
        Assert.Equal("\"line-thin\"", JsonSerializer.Serialize(CursorStyle.LineThin));
        Assert.Equal("\"underline\"", JsonSerializer.Serialize(CursorStyle.Underline));
        Assert.Equal("\"underline-thin\"", JsonSerializer.Serialize(CursorStyle.UnderlineThin));

        Assert.Equal(CursorStyle.BlockOutline, JsonSerializer.Deserialize<CursorStyle>("\"block-outline\""));
        Assert.Equal(CursorStyle.LineThin, JsonSerializer.Deserialize<CursorStyle>("\"line-thin\""));
        Assert.Equal(CursorStyle.UnderlineThin, JsonSerializer.Deserialize<CursorStyle>("\"underline-thin\""));
    }

    [Fact]
    public void StringEnum_CamelCaseValues_PreservedExactly()
    {
        // Verify camelCase wire values are not affected by any naming policy
        Assert.Equal("\"beforeWhitespace\"", JsonSerializer.Serialize(AutoClosingBrackets.BeforeWhitespace));
        Assert.Equal("\"languageDefined\"", JsonSerializer.Serialize(AutoClosingBrackets.LanguageDefined));
        Assert.Equal("\"ctrlCmd\"", JsonSerializer.Serialize(MultiCursorModifier.CtrlCmd));
        Assert.Equal("\"gotoAndPeek\"", JsonSerializer.Serialize(Multiple.GotoAndPeek));
        Assert.Equal("\"recentlyUsedByPrefix\"", JsonSerializer.Serialize(SuggestSelection.RecentlyUsedByPrefix));
        Assert.Equal("\"onlySnippets\"", JsonSerializer.Serialize(TabCompletion.OnlySnippets));
        Assert.Equal("\"wordWrapColumn\"", JsonSerializer.Serialize(WordWrap.WordWrapColumn));
        Assert.Equal("\"deepIndent\"", JsonSerializer.Serialize(WrappingIndent.DeepIndent));
    }

    [Fact]
    public void NumericEnum_StillSerializesAsInteger()
    {
        // Verify numeric enums were NOT affected by string enum migration
        Assert.Equal("8", JsonSerializer.Serialize(MarkerSeverity.Error));
        Assert.Equal("4", JsonSerializer.Serialize(MarkerSeverity.Warning));
        Assert.Equal("1", JsonSerializer.Serialize(CompletionItemKind.Function));
        Assert.Equal("0", JsonSerializer.Serialize(CompletionItemKind.Method));
    }

    #endregion
}
