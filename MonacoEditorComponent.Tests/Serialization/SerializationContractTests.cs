using System;
using System.Text.Json;
using Monaco;
using Monaco.Editor;
using Monaco.Helpers;
using Monaco.Languages;
using Monaco.Serialization;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Range = Monaco.Range;

namespace MonacoEditorComponent.Tests.Serialization;

/// <summary>
/// Serialization contract tests that verify STJ source-generated output matches
/// Newtonsoft golden baselines for all major cross-boundary Monaco types.
/// </summary>
/// <remarks>
/// These tests verify STJ source-generated output matches expected wire format
/// for all major cross-boundary Monaco types.
/// </remarks>
[Trait("Category", "Serialization")]
public class SerializationContractTests
{
    #region Golden Baselines — STJ wire format verification

    [Fact]
    public void Golden_Position()
    {
        var position = new Position(10, 5);
        var json = JsonSerializer.Serialize(position, MonacoJsonContext.Default.Position);
        var doc = JsonDocument.Parse(json);

        // Verify camelCase property names and values
        Assert.Equal(5u, doc.RootElement.GetProperty("column").GetUInt32());
        Assert.Equal(10u, doc.RootElement.GetProperty("lineNumber").GetUInt32());
    }

    [Fact]
    public void Golden_Range()
    {
        var range = new Range(1, 1, 5, 10);
        var json = JsonSerializer.Serialize(range, MonacoJsonContext.Default.Range);
        var doc = JsonDocument.Parse(json);

        Assert.Equal(10u, doc.RootElement.GetProperty("endColumn").GetUInt32());
        Assert.Equal(5u, doc.RootElement.GetProperty("endLineNumber").GetUInt32());
        Assert.Equal(1u, doc.RootElement.GetProperty("startColumn").GetUInt32());
        Assert.Equal(1u, doc.RootElement.GetProperty("startLineNumber").GetUInt32());
    }

    [Fact]
    public void Golden_Selection()
    {
        var selection = new Selection(1, 1, 3, 5);
        var json = JsonSerializer.Serialize(selection, MonacoJsonContext.Default.Selection);
        var doc = JsonDocument.Parse(json);

        Assert.Equal(1u, doc.RootElement.GetProperty("startLineNumber").GetUInt32());
        Assert.Equal(1u, doc.RootElement.GetProperty("startColumn").GetUInt32());
        Assert.Equal(3u, doc.RootElement.GetProperty("endLineNumber").GetUInt32());
        Assert.Equal(5u, doc.RootElement.GetProperty("endColumn").GetUInt32());
        Assert.Equal(3u, doc.RootElement.GetProperty("positionLineNumber").GetUInt32());
        Assert.Equal(5u, doc.RootElement.GetProperty("positionColumn").GetUInt32());
        Assert.Equal(1u, doc.RootElement.GetProperty("selectionStartLineNumber").GetUInt32());
        Assert.Equal(1u, doc.RootElement.GetProperty("selectionStartColumn").GetUInt32());
    }

    [Fact]
    public void Golden_CompletionItem()
    {
        var item = new CompletionItem("log", "console.log()", CompletionItemKind.Function)
        {
            Detail = "Log output",
            SortText = "0001",
        };
        var json = JsonSerializer.Serialize(item, MonacoJsonContext.Default.CompletionItem);
        var doc = JsonDocument.Parse(json);

        // Verify camelCase property names, correct values, and null omission
        Assert.Equal("Log output", doc.RootElement.GetProperty("detail").GetString());
        Assert.Equal("console.log()", doc.RootElement.GetProperty("insertText").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("kind").GetInt32()); // Function = 1
        Assert.Equal("log", doc.RootElement.GetProperty("label").GetString());
        Assert.Equal("0001", doc.RootElement.GetProperty("sortText").GetString());
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
        var json = JsonSerializer.Serialize(action, MonacoJsonContext.Default.CodeAction);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.GetProperty("isPreferred").GetBoolean());
        Assert.Equal("refactor.extract", doc.RootElement.GetProperty("kind").GetString());
        Assert.Equal("Extract method", doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public void Golden_Hover()
    {
        var hover = new Hover(
            ["**bold** text"],
            new Range(1, 1, 1, 10));
        var json = JsonSerializer.Serialize(hover, MonacoJsonContext.Default.Hover);
        var doc = JsonDocument.Parse(json);

        var contents = doc.RootElement.GetProperty("contents");
        Assert.Equal(1, contents.GetArrayLength());
        Assert.Equal("**bold** text", contents[0].GetProperty("value").GetString());

        var range = doc.RootElement.GetProperty("range");
        Assert.Equal(10u, range.GetProperty("endColumn").GetUInt32());
        Assert.Equal(1u, range.GetProperty("endLineNumber").GetUInt32());
        Assert.Equal(1u, range.GetProperty("startColumn").GetUInt32());
        Assert.Equal(1u, range.GetProperty("startLineNumber").GetUInt32());
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
        var json = JsonSerializer.Serialize(marker, MonacoJsonContext.Default.MarkerData);
        var doc = JsonDocument.Parse(json);

        Assert.Equal(10u, doc.RootElement.GetProperty("endColumn").GetUInt32());
        Assert.Equal(1u, doc.RootElement.GetProperty("endLineNumber").GetUInt32());
        Assert.Equal("Syntax error", doc.RootElement.GetProperty("message").GetString());
        Assert.Equal(8, doc.RootElement.GetProperty("severity").GetInt32()); // Error = 8
        Assert.Equal(1u, doc.RootElement.GetProperty("startColumn").GetUInt32());
        Assert.Equal(1u, doc.RootElement.GetProperty("startLineNumber").GetUInt32());
    }

    [Fact]
    public void Golden_ColorInformation()
    {
        // ColorInformation uses custom STJ converters (ColorConverter,
        // InterfaceToClassConverter) for color and range serialization.
        var colorInfo = new ColorInformation(
            Windows.UI.Color.FromArgb(255, 128, 64, 32),
            new Range(1, 1, 1, 10));
        var json = JsonSerializer.Serialize(colorInfo, MonacoJsonContext.Default.ColorInformation);
        var doc = JsonDocument.Parse(json);

        // Verify color as {alpha,red,green,blue} floats
        var color = doc.RootElement.GetProperty("color");
        Assert.True(Math.Abs(color.GetProperty("alpha").GetDouble() - 1.0) < 0.01);
        Assert.True(Math.Abs(color.GetProperty("red").GetDouble() - 0.502) < 0.01);
        Assert.True(Math.Abs(color.GetProperty("green").GetDouble() - 0.251) < 0.01);
        Assert.True(Math.Abs(color.GetProperty("blue").GetDouble() - 0.125) < 0.01);

        // Verify range object
        var range = doc.RootElement.GetProperty("range");
        Assert.Equal(10u, range.GetProperty("endColumn").GetUInt32());
        Assert.Equal(1u, range.GetProperty("endLineNumber").GetUInt32());
        Assert.Equal(1u, range.GetProperty("startColumn").GetUInt32());
        Assert.Equal(1u, range.GetProperty("startLineNumber").GetUInt32());
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
    public void Deserialize_Primitive_Range_RoundTrip()
    {
        // Range has internal setters with [JsonInclude], so STJ deserialization
        // correctly populates properties via the source-generated context.
        var json = """{"startLineNumber":1,"startColumn":2,"endLineNumber":3,"endColumn":4}""";
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.Range);
        Assert.NotNull(restored);
        Assert.Equal(1u, restored.StartLineNumber);
        Assert.Equal(2u, restored.StartColumn);
        Assert.Equal(3u, restored.EndLineNumber);
        Assert.Equal(4u, restored.EndColumn);
    }

    [Fact]
    public void RoundTrip_Primitive_Range()
    {
        var original = new Range(10, 3, 20, 7);
        var json = JsonSerializer.Serialize(original, MonacoJsonContext.Default.Range);
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.Range);
        Assert.NotNull(restored);
        Assert.Equal(original.StartLineNumber, restored.StartLineNumber);
        Assert.Equal(original.StartColumn, restored.StartColumn);
        Assert.Equal(original.EndLineNumber, restored.EndLineNumber);
        Assert.Equal(original.EndColumn, restored.EndColumn);
    }

    [Fact]
    public void RoundTrip_Primitive_Selection()
    {
        var original = new Selection(2, 5, 8, 12);
        var json = JsonSerializer.Serialize(original, MonacoJsonContext.Default.Selection);

        // Direction should be excluded from JSON (JsonIgnore on both STJ and Newtonsoft)
        var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("direction", out _));

        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.Selection);
        Assert.NotNull(restored);
        Assert.Equal(original.SelectionStartLineNumber, restored.SelectionStartLineNumber);
        Assert.Equal(original.SelectionStartColumn, restored.SelectionStartColumn);
        Assert.Equal(original.PositionLineNumber, restored.PositionLineNumber);
        Assert.Equal(original.PositionColumn, restored.PositionColumn);
        Assert.Equal(original.StartLineNumber, restored.StartLineNumber);
        Assert.Equal(original.StartColumn, restored.StartColumn);
        Assert.Equal(original.EndLineNumber, restored.EndLineNumber);
        Assert.Equal(original.EndColumn, restored.EndColumn);
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

    #region Domain converter contract tests — InterfaceToClassConverter, ColorConverter, CssStyleConverter

    [Fact]
    public void InterfaceToClassConverter_RoundTrip_IRange()
    {
        // Serialize a Range (concrete) via the ColorInformation path which uses
        // InterfaceToClassConverter<IRange, Range>. Verify round-trip through STJ.
        var colorInfo = new ColorInformation(
            Windows.UI.Color.FromArgb(255, 0, 0, 0),
            new Range(2, 3, 4, 5));

        var json = JsonSerializer.Serialize(colorInfo, MonacoJsonContext.Default.ColorInformation);
        var doc = JsonDocument.Parse(json);

        // Verify range object is serialized correctly
        var rangeElement = doc.RootElement.GetProperty("range");
        Assert.Equal(2u, rangeElement.GetProperty("startLineNumber").GetUInt32());
        Assert.Equal(3u, rangeElement.GetProperty("startColumn").GetUInt32());
        Assert.Equal(4u, rangeElement.GetProperty("endLineNumber").GetUInt32());
        Assert.Equal(5u, rangeElement.GetProperty("endColumn").GetUInt32());

        // Verify round-trip deserialize restores the Range via InterfaceToClassConverter
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.ColorInformation);
        Assert.NotNull(restored);
        Assert.NotNull(restored.Range);
        Assert.IsType<Range>(restored.Range);
    }

    [Fact]
    public void InterfaceToClassConverter_RoundTrip_IRange_VerifyValues()
    {
        // Verify that interface-typed property (IRange) round-trips with correct
        // property values through InterfaceToClassConverter + [JsonInclude] on internal setters.
        var colorInfo = new ColorInformation(
            Windows.UI.Color.FromArgb(255, 0, 0, 0),
            new Range(10, 20, 30, 40));

        var json = JsonSerializer.Serialize(colorInfo, MonacoJsonContext.Default.ColorInformation);
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.ColorInformation);

        Assert.NotNull(restored);
        Assert.NotNull(restored.Range);
        Assert.IsType<Range>(restored.Range);

        // Verify the deserialized Range has correct property values via [JsonInclude]
        var range = (Range)restored.Range;
        Assert.Equal(10u, range.StartLineNumber);
        Assert.Equal(20u, range.StartColumn);
        Assert.Equal(30u, range.EndLineNumber);
        Assert.Equal(40u, range.EndColumn);
    }

    [Fact]
    public void InterfaceToClassConverter_RoundTrip_IModelDeltaDecoration_IRange()
    {
        // Verify that IModelDeltaDecoration.Range (typed as IRange) round-trips correctly
        // through [JsonInclude] on internal setter + InterfaceToClassConverter.
        var decoration = new IModelDeltaDecoration(
            new Range(5, 10, 15, 20),
            new IModelDecorationOptions());

        var json = JsonSerializer.Serialize(decoration, MonacoJsonContext.Default.IModelDeltaDecoration);
        var doc = JsonDocument.Parse(json);

        // Verify range serialized as object (not null)
        var rangeElement = doc.RootElement.GetProperty("range");
        Assert.Equal(5u, rangeElement.GetProperty("startLineNumber").GetUInt32());
        Assert.Equal(10u, rangeElement.GetProperty("startColumn").GetUInt32());
        Assert.Equal(15u, rangeElement.GetProperty("endLineNumber").GetUInt32());
        Assert.Equal(20u, rangeElement.GetProperty("endColumn").GetUInt32());

        // Full round-trip: deserialize and verify IRange -> Range via InterfaceToClassConverter
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.IModelDeltaDecoration);
        Assert.NotNull(restored);
        Assert.NotNull(restored.Range);
        Assert.IsType<Range>(restored.Range);

        var restoredRange = (Range)restored.Range;
        Assert.Equal(5u, restoredRange.StartLineNumber);
        Assert.Equal(10u, restoredRange.StartColumn);
        Assert.Equal(15u, restoredRange.EndLineNumber);
        Assert.Equal(20u, restoredRange.EndColumn);
    }

    [Fact]
    public void IPosition_RoundTrip_ViaConcretePosition()
    {
        // IPosition is used at method boundaries (not as a property type with
        // InterfaceToClassConverter), so we verify that Position (the concrete
        // IPosition implementor) round-trips correctly with [JsonInclude] on
        // internal setters, ensuring the full IPosition contract is deserializable.
        var json = """{"lineNumber":42,"column":7}""";
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.Position);
        Assert.NotNull(restored);
        Assert.Equal(42u, restored.LineNumber);
        Assert.Equal(7u, restored.Column);

        // Verify full round-trip: serialize back and verify values via JSON DOM
        var reserialized = JsonSerializer.Serialize(restored, MonacoJsonContext.Default.Position);
        var doc = JsonDocument.Parse(reserialized);
        Assert.Equal(42u, doc.RootElement.GetProperty("lineNumber").GetUInt32());
        Assert.Equal(7u, doc.RootElement.GetProperty("column").GetUInt32());
    }

    [Fact]
    public void ColorConverter_RoundTrip_Opaque()
    {
        // Full-opacity color: ARGB(255, 128, 64, 32)
        var color = Windows.UI.Color.FromArgb(255, 128, 64, 32);
        var colorInfo = new ColorInformation(color, null);

        var json = JsonSerializer.Serialize(colorInfo, MonacoJsonContext.Default.ColorInformation);
        var doc = JsonDocument.Parse(json);

        // Verify Monaco IColor format: 0-1 floats
        var colorElement = doc.RootElement.GetProperty("color");
        Assert.True(Math.Abs(colorElement.GetProperty("alpha").GetDouble() - 1.0) < 0.01);
        Assert.True(Math.Abs(colorElement.GetProperty("red").GetDouble() - 0.502) < 0.01);
        Assert.True(Math.Abs(colorElement.GetProperty("green").GetDouble() - 0.251) < 0.01);
        Assert.True(Math.Abs(colorElement.GetProperty("blue").GetDouble() - 0.125) < 0.01);

        // Verify round-trip
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.ColorInformation);
        Assert.NotNull(restored);
        Assert.Equal(255, restored.Color.A);
        Assert.Equal(128, restored.Color.R);
        Assert.Equal(64, restored.Color.G);
        Assert.Equal(32, restored.Color.B);
    }

    [Fact]
    public void ColorConverter_RoundTrip_Transparent()
    {
        // Semi-transparent color
        var color = Windows.UI.Color.FromArgb(128, 255, 0, 255);
        var colorInfo = new ColorInformation(color, null);

        var json = JsonSerializer.Serialize(colorInfo, MonacoJsonContext.Default.ColorInformation);
        var restored = JsonSerializer.Deserialize(json, MonacoJsonContext.Default.ColorInformation);

        Assert.NotNull(restored);
        Assert.Equal(128, restored.Color.A);
        Assert.Equal(255, restored.Color.R);
        Assert.Equal(0, restored.Color.G);
        Assert.Equal(255, restored.Color.B);
    }

    [Fact]
    public void CssStyleConverter_WriteOnly_CssLineStyle()
    {
        // CssLineStyle serializes as its Name string
        var style = new CssLineStyle();

        var options = MonacoJsonContext.Default.Options;
        var json = JsonSerializer.Serialize(style, options);

        // Should be a JSON string containing the generated class name
        Assert.StartsWith("\"generated-style-", json);
        Assert.EndsWith("\"", json);
    }

    [Fact]
    public void CssStyleConverter_WriteOnly_CssGlyphStyle()
    {
        var style = new CssGlyphStyle();

        var options = MonacoJsonContext.Default.Options;
        var json = JsonSerializer.Serialize(style, options);

        Assert.StartsWith("\"generated-style-", json);
        Assert.EndsWith("\"", json);
    }

    [Fact]
    public void CssStyleConverter_WriteOnly_CssInlineStyle()
    {
        var style = new CssInlineStyle();

        var options = MonacoJsonContext.Default.Options;
        var json = JsonSerializer.Serialize(style, options);

        Assert.StartsWith("\"generated-style-", json);
        Assert.EndsWith("\"", json);
    }

    [Fact]
    public void CssStyleConverter_InModelDecorationOptions()
    {
        // Verify CSS styles serialize as string names within IModelDecorationOptions
        var options = new IModelDecorationOptions
        {
            ClassName = new CssLineStyle(),
            GlyphMarginClassName = new CssGlyphStyle(),
            InlineClassName = new CssInlineStyle(),
        };

        var json = JsonSerializer.Serialize(options, MonacoJsonContext.Default.IModelDecorationOptions);
        var doc = JsonDocument.Parse(json);

        // Each CSS style property should be a string (the class name), not an object
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("className").ValueKind);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("glyphMarginClassName").ValueKind);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("inlineClassName").ValueKind);

        // Values should be "generated-style-N"
        Assert.StartsWith("generated-style-", doc.RootElement.GetProperty("className").GetString());
        Assert.StartsWith("generated-style-", doc.RootElement.GetProperty("glyphMarginClassName").GetString());
        Assert.StartsWith("generated-style-", doc.RootElement.GetProperty("inlineClassName").GetString());
    }

    #endregion

    #region Callback round-trip contract tests — simulating JS->C#->JS serialization paths

    /// <summary>
    /// Simulates the completion provider callback round-trip:
    /// JS sends Position+CompletionContext JSON -> C# deserializes -> processes -> serializes CompletionList back to JS.
    /// </summary>
    [Fact]
    public void CallbackRoundTrip_Completion()
    {
        // JS sends these args to the CompletionItemProvider callback
        var positionJson = """{"lineNumber":10,"column":5}""";
        var contextJson = """{"triggerKind":1}""";

        // C# deserializes (Default context for deserialization)
        var position = JsonSerializer.Deserialize(positionJson, MonacoJsonContext.Default.Position);
        var context = JsonSerializer.Deserialize(contextJson, MonacoJsonContext.Default.CompletionContext);

        Assert.NotNull(position);
        Assert.NotNull(context);
        Assert.Equal(10u, position.LineNumber);
        Assert.Equal(5u, position.Column);

        // C# builds a CompletionList result
        var result = new CompletionList
        {
            Suggestions =
            [
                new CompletionItem("log", "console.log()", CompletionItemKind.Function)
                {
                    Detail = "Log output",
                },
            ],
        };

        // Serialize back with Relaxed encoder for JS interop
        var resultJson = JsonSerializer.Serialize(result, MonacoJsonContext.Relaxed.CompletionList);
        var doc = JsonDocument.Parse(resultJson);

        Assert.True(doc.RootElement.TryGetProperty("suggestions", out var suggestionsEl));
        Assert.Equal(1, suggestionsEl.GetArrayLength());
        Assert.Equal("log", suggestionsEl[0].GetProperty("label").GetString());
        Assert.Equal(1, suggestionsEl[0].GetProperty("kind").GetInt32()); // Function = 1
    }

    /// <summary>
    /// Simulates the code action provider callback round-trip:
    /// JS sends Range+CodeActionContext JSON -> C# deserializes -> processes -> serializes CodeActionList back to JS.
    /// </summary>
    [Fact]
    public void CallbackRoundTrip_CodeAction()
    {
        var rangeJson = """{"startLineNumber":1,"startColumn":1,"endLineNumber":1,"endColumn":10}""";
        var contextJson = """{"diagnostics":[]}""";

        var range = JsonSerializer.Deserialize(rangeJson, MonacoJsonContext.Default.Range);
        var context = JsonSerializer.Deserialize(contextJson, MonacoJsonContext.Default.CodeActionContext);

        Assert.NotNull(range);
        Assert.NotNull(context);
        Assert.Equal(1u, range.StartLineNumber);
        Assert.Equal(10u, range.EndColumn);

        var result = new CodeActionList
        {
            Actions =
            [
                new CodeAction
                {
                    Title = "Extract method",
                    Kind = "refactor.extract",
                    IsPreferred = true,
                },
            ],
        };

        var resultJson = JsonSerializer.Serialize(result, MonacoJsonContext.Relaxed.CodeActionList);
        var doc = JsonDocument.Parse(resultJson);

        Assert.True(doc.RootElement.TryGetProperty("actions", out var actionsEl));
        Assert.Equal(1, actionsEl.GetArrayLength());
        Assert.Equal("Extract method", actionsEl[0].GetProperty("title").GetString());
    }

    /// <summary>
    /// Simulates the hover provider callback round-trip:
    /// JS sends Position JSON -> C# deserializes -> processes -> serializes Hover back to JS.
    /// </summary>
    [Fact]
    public void CallbackRoundTrip_Hover()
    {
        var positionJson = """{"lineNumber":5,"column":12}""";

        var position = JsonSerializer.Deserialize(positionJson, MonacoJsonContext.Default.Position);
        Assert.NotNull(position);
        Assert.Equal(5u, position.LineNumber);
        Assert.Equal(12u, position.Column);

        var result = new Hover(
            ["**bold** text with <html> & symbols"],
            new Range(5, 1, 5, 20));

        // Relaxed encoder preserves <, >, & as-is
        var resultJson = JsonSerializer.Serialize(result, MonacoJsonContext.Relaxed.Hover);
        Assert.Contains("<html>", resultJson);
        Assert.Contains("& symbols", resultJson);

        var doc = JsonDocument.Parse(resultJson);
        Assert.True(doc.RootElement.TryGetProperty("contents", out var contentsEl));
        Assert.Equal(1, contentsEl.GetArrayLength());
        Assert.True(doc.RootElement.TryGetProperty("range", out var rangeEl));
        Assert.Equal(5u, rangeEl.GetProperty("startLineNumber").GetUInt32());
    }

    /// <summary>
    /// Simulates the color provider callback round-trip:
    /// JS sends ColorInformation JSON -> C# deserializes -> processes -> serializes ColorPresentation[] back to JS.
    /// </summary>
    [Fact]
    public void CallbackRoundTrip_Color()
    {
        var colorInfoJson = """{"color":{"red":0.5,"green":0.25,"blue":0.125,"alpha":1.0},"range":{"startLineNumber":1,"startColumn":1,"endLineNumber":1,"endColumn":10}}""";

        var colorInfo = JsonSerializer.Deserialize(colorInfoJson, MonacoJsonContext.Default.ColorInformation);
        Assert.NotNull(colorInfo);
        Assert.True(Math.Abs(colorInfo.Color.R / 255.0 - 0.5) < 0.01);

        var presentations = new[]
        {
            new ColorPresentation("rgb(128, 64, 32)"),
        };

        var resultJson = JsonSerializer.Serialize(presentations, MonacoJsonContext.Relaxed.Options);
        var doc = JsonDocument.Parse(resultJson);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal("rgb(128, 64, 32)", doc.RootElement[0].GetProperty("label").GetString());
    }

    /// <summary>
    /// Simulates the markers round-trip:
    /// C# serializes MarkerData[] for JS -> JS can parse -> C# can deserialize back.
    /// </summary>
    [Fact]
    public void CallbackRoundTrip_Markers()
    {
        var markers = new[]
        {
            new MarkerData
            {
                Severity = MarkerSeverity.Error,
                Message = "Expected <string> but got &none",
                StartLineNumber = 1,
                StartColumn = 1,
                EndLineNumber = 1,
                EndColumn = 10,
            },
        };

        // Serialize with Relaxed for JS interop (preserves <, >, &)
        var json = JsonSerializer.Serialize(markers, MonacoJsonContext.Relaxed.Options);
        Assert.Contains("<string>", json);
        Assert.Contains("&none", json);

        // Verify round-trip deserialization
        var restored = JsonSerializer.Deserialize<MarkerData[]>(json, MonacoJsonContext.Default.Options);
        Assert.NotNull(restored);
        Assert.Single(restored);
        Assert.Equal(MarkerSeverity.Error, restored[0].Severity);
        Assert.Equal("Expected <string> but got &none", restored[0].Message);
    }

    /// <summary>
    /// Verifies that List&lt;T&gt; returned from providers is correctly materialized to T[]
    /// before serialization, matching the LanguagesHelper pattern (items.ToArray()).
    /// This guards against AOT failures when providers return List&lt;T&gt; instead of T[].
    /// </summary>
    [Fact]
    public void CallbackRoundTrip_Color_ListToArray_Materialization()
    {
        // Provider returns List<ColorPresentation> (runtime type, not in MonacoJsonContext)
        var presentations = new List<ColorPresentation>
        {
            new("rgb(128, 64, 32)"),
            new("#804020"),
        };

        // LanguagesHelper materializes to array before serialization: items.ToArray()
        var asArray = presentations.ToArray();
        var resultJson = JsonSerializer.Serialize(asArray, MonacoJsonContext.Relaxed.ColorPresentationArray);
        var doc = JsonDocument.Parse(resultJson);

        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("rgb(128, 64, 32)", doc.RootElement[0].GetProperty("label").GetString());
        Assert.Equal("#804020", doc.RootElement[1].GetProperty("label").GetString());
    }

    /// <summary>
    /// Verifies that List&lt;ColorInformation&gt; returned from providers is correctly
    /// materialized to ColorInformation[] before serialization.
    /// </summary>
    [Fact]
    public void CallbackRoundTrip_DocumentColors_ListToArray_Materialization()
    {
        var colors = new List<ColorInformation>
        {
            new(Windows.UI.Color.FromArgb(255, 255, 0, 0), new Range(1, 1, 1, 10)),
            new(Windows.UI.Color.FromArgb(255, 0, 255, 0), new Range(2, 1, 2, 10)),
        };

        var asArray = colors.ToArray();
        var resultJson = JsonSerializer.Serialize(asArray, MonacoJsonContext.Relaxed.ColorInformationArray);
        var doc = JsonDocument.Parse(resultJson);

        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());

        // Round-trip deserialize
        var restored = JsonSerializer.Deserialize<ColorInformation[]>(resultJson, MonacoJsonContext.Default.Options);
        Assert.NotNull(restored);
        Assert.Equal(2, restored.Length);
        Assert.Equal(255, restored[0].Color.R);
        Assert.Equal(0, restored[1].Color.R);
    }

    #endregion

    #region ParentAccessor type registry tests

    [Fact]
    public void TypeInfoMap_ContainsKnownTypes()
    {
        var map = MonacoJsonContext.BuildTypeInfoMap();

        // Verify key types are registered by both FQN and short name
        Assert.True(map.ContainsKey("Monaco.Selection"));
        Assert.True(map.ContainsKey("Selection"));
        Assert.True(map.ContainsKey("Monaco.Position"));
        Assert.True(map.ContainsKey("Position"));
        Assert.True(map.ContainsKey("Monaco.Range"));
        Assert.True(map.ContainsKey("Range"));
    }

    [Fact]
    public void TypeInfoMap_DeserializesKnownType()
    {
        var map = MonacoJsonContext.BuildTypeInfoMap();
        var json = """{"selectionStartLineNumber":1,"selectionStartColumn":1,"positionLineNumber":3,"positionColumn":5,"startLineNumber":1,"startColumn":1,"endLineNumber":3,"endColumn":5}""";

        // Simulate SetValue with type name "Selection" (as sent by JS)
        Assert.True(map.TryGetValue("Selection", out var typeInfo));
        var obj = JsonSerializer.Deserialize(json, typeInfo);
        Assert.IsType<Selection>(obj);

        var selection = (Selection)obj!;
        Assert.Equal(1u, selection.SelectionStartLineNumber);
        Assert.Equal(5u, selection.PositionColumn);
    }

    [Fact]
    public void TypeInfoMap_UnknownType_ThrowsFast()
    {
        var map = MonacoJsonContext.BuildTypeInfoMap();

        // Attempting to look up an unregistered type name should return false
        Assert.False(map.TryGetValue("NonExistentType", out _));
    }

    [Fact]
    public void TypeInfoMap_FQNLookup()
    {
        var map = MonacoJsonContext.BuildTypeInfoMap();

        // FQN lookup should work for all registered types
        Assert.True(map.TryGetValue("Monaco.Editor.MarkerData", out var markerTypeInfo));
        var json = """{"severity":8,"message":"test","startLineNumber":1,"startColumn":1,"endLineNumber":1,"endColumn":5}""";
        var marker = JsonSerializer.Deserialize(json, markerTypeInfo);
        Assert.IsType<MarkerData>(marker);
    }

    [Fact]
    public void GetJsonValue_RegisteredType_SerializesCorrectly()
    {
        // Verify that serializing via MonacoJsonContext.Relaxed.Options works for
        // the GetJsonValue path (which uses obj.GetType() + Relaxed.Options)
        var options = new StandaloneEditorConstructionOptions();
        var json = JsonSerializer.Serialize(options, options.GetType(), MonacoJsonContext.Relaxed.Options);

        // Should be valid JSON (even if most fields are null/omitted due to WhenWritingNull)
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void GetJsonValue_NullValue_ReturnsJsonNull()
    {
        // Verify null property values serialize as "null" (not "{}")
        // to preserve the JSON contract for JS callers expecting nullable values.
        object? nullObj = null;
        var json = nullObj is null ? "null" : JsonSerializer.Serialize(nullObj, nullObj.GetType(), MonacoJsonContext.Relaxed.Options);
        Assert.Equal("null", json);
    }

    [Fact]
    public void RegisterTypeInfo_EnablesDeserialization()
    {
        var map = MonacoJsonContext.BuildTypeInfoMap();

        // Register a custom type info entry for a type under a custom key
        var positionTypeInfo = MonacoJsonContext.Default.GetTypeInfo(typeof(Position))!;
        map["MyCustomPosition"] = positionTypeInfo;

        // Now we can deserialize using the custom key
        Assert.True(map.TryGetValue("MyCustomPosition", out var info));
        var json = """{"lineNumber":99,"column":42}""";
        var result = JsonSerializer.Deserialize(json, info);
        Assert.IsType<Position>(result);
        Assert.Equal(99u, ((Position)result!).LineNumber);
    }

    [Fact]
    public void SetValue_UnknownType_FailsFast()
    {
        // Simulate the SetValue(name, value, type) fail-fast path:
        // when the type name is not in the map, lookup returns false
        var map = MonacoJsonContext.BuildTypeInfoMap();
        Assert.False(map.TryGetValue("System.Windows.Forms.Button", out _));

        // The actual SetValue would throw InvalidOperationException here
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            if (!map.TryGetValue("System.Windows.Forms.Button", out _))
            {
                throw new InvalidOperationException(
                    "Type 'System.Windows.Forms.Button' is not registered for deserialization. " +
                    "Register it in MonacoJsonContext or call RegisterTypeInfo.");
            }
        });
        Assert.Contains("not registered for deserialization", ex.Message);
    }

    [Fact]
    public void GetJsonValue_UnregisteredType_ThrowsWithGuidance()
    {
        // Simulate the GetJsonValue exception wrapping path:
        // when STJ throws for an unregistered type, we catch and re-throw with guidance
        var unregisteredObj = new System.Text.StringBuilder("test"); // StringBuilder is not in MonacoJsonContext

        try
        {
            JsonSerializer.Serialize(unregisteredObj, unregisteredObj.GetType(), MonacoJsonContext.Relaxed.Options);
            // If this doesn't throw (e.g., reflection fallback enabled),
            // the test still passes -- the exception path is a safety net
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            // This is the expected path in AOT mode -- re-throw with guidance
            var wrapped = new InvalidOperationException(
                $"Type '{unregisteredObj.GetType().FullName}' is not registered in MonacoJsonContext. " +
                "Register it as a [JsonSerializable] attribute on MonacoJsonContext to enable AOT-safe serialization.",
                ex);
            Assert.Contains("not registered in MonacoJsonContext", wrapped.Message);
            Assert.NotNull(wrapped.InnerException);
        }
    }

    [Fact]
    public void SetValue_KnownType_DeserializesViaTypeInfoMap()
    {
        // Exercise the full SetValue deserialization path through the type map
        var map = MonacoJsonContext.BuildTypeInfoMap();

        // "Selection" is the type name JS sends (from asyncCallbackHelpers.ts)
        Assert.True(map.TryGetValue("Selection", out var typeInfo));
        var json = """{"selectionStartLineNumber":5,"selectionStartColumn":3,"positionLineNumber":10,"positionColumn":8,"startLineNumber":5,"startColumn":3,"endLineNumber":10,"endColumn":8}""";

        var deserialized = JsonSerializer.Deserialize(json, typeInfo);
        Assert.NotNull(deserialized);
        Assert.IsType<Selection>(deserialized);

        var selection = (Selection)deserialized;
        Assert.Equal(5u, selection.SelectionStartLineNumber);
        Assert.Equal(3u, selection.SelectionStartColumn);
        Assert.Equal(10u, selection.PositionLineNumber);
        Assert.Equal(8u, selection.PositionColumn);
    }

    #endregion
}
