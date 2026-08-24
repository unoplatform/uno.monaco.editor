using System.Text.Json;

using Monaco;
using Monaco.Editor;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Pins the argument order of the <c>model.find*</c> scripts.
///
/// <para>These calls used to go through <c>InvokeScriptAsync(method, args)</c>, which emits
/// <c>method(element, ...args)</c> so the host can resolve the editor element. That is correct for
/// the helper functions in <c>ts-helpermethods</c> (they all take the element first) but wrong for a
/// Monaco API, where it shifts every argument by one: Monaco read the DOM element as the search
/// pattern -- stringified to <c>[object HTMLDivElement]</c>, a character class that matches an
/// isolated space -- and <c>captureMatches</c> as the result limit. Every search therefore reported
/// one bogus match at the first space-delimited character, which the sample app's color provider
/// turned into a black colorpicker decoration.</para>
/// </summary>
public sealed class ModelHelperScriptTests
{
    [Fact]
    public void BuildFindMatchesScript_PassesArgumentsInMonacoOrder()
    {
        var script = ModelHelper.BuildFindMatchesScript(
            searchString: "#[A-Fa-f0-9]{8}",
            searchOnlyEditableRange: true,
            isRegex: true,
            matchCase: true,
            wordSeparators: null,
            captureMatches: true,
            limitResultCount: 999);

        Assert.Equal(
            "EditorContext.getEditorForElement(element).model.findMatches(\"#[A-Fa-f0-9]{8}\", true, true, true, null, true, 999);",
            script);
    }

    [Fact]
    public void BuildFindMatchesScript_DoesNotPassTheElementAsTheSearchPattern()
    {
        var script = ModelHelper.BuildFindMatchesScript(
            searchString: "needle",
            searchOnlyEditableRange: true,
            isRegex: false,
            matchCase: false,
            wordSeparators: null,
            captureMatches: false,
            limitResultCount: 999);

        Assert.StartsWith(
            "EditorContext.getEditorForElement(element).model.findMatches(\"needle\"",
            script);
        Assert.DoesNotContain("findMatches(element", script);
    }

    [Fact]
    public void BuildFindMatchesScript_SerializesWordSeparatorsAsAString()
    {
        var script = ModelHelper.BuildFindMatchesScript(
            searchString: "needle",
            searchOnlyEditableRange: true,
            isRegex: false,
            matchCase: false,
            wordSeparators: " \t.,",
            captureMatches: false,
            limitResultCount: 100);

        Assert.Contains("\" \\t.,\"", script);
        Assert.EndsWith(", 100);", script);
    }

    /// <summary>
    /// The pattern is interpolated into a script that the WASM host evaluates, so it has to survive
    /// as a JS string literal. Before the argument shift was fixed no caller-supplied pattern ever
    /// reached Monaco intact, which makes this the first path where the escaping actually matters.
    /// </summary>
    [Fact]
    public void BuildFindMatchesScript_EscapesCharactersThatWouldBreakTheScript()
    {
        var lineSeparator = (char)0x2028;
        var paragraphSeparator = (char)0x2029;
        var lineFeed = (char)10;
        var pattern = $"a{lineSeparator}b{paragraphSeparator}c{lineFeed}d\"e\\f";

        var script = ModelHelper.BuildFindMatchesScript(
            pattern,
            searchOnlyEditableRange: true,
            isRegex: true,
            matchCase: true,
            wordSeparators: null,
            captureMatches: true,
            limitResultCount: 999);

        // A raw line terminator (LF, U+2028 or U+2029) inside the literal is a JS syntax error.
        Assert.DoesNotContain(lineSeparator, script);
        Assert.DoesNotContain(paragraphSeparator, script);
        Assert.DoesNotContain(lineFeed, script);

        // ... and the emitted literal still has to decode back to the caller's pattern, so
        // quotes and backslashes cannot have ended it early.
        const string prefix = "EditorContext.getEditorForElement(element).model.findMatches(";
        var literal = script[prefix.Length..];
        literal = literal[..literal.IndexOf(", true, true, true, null, true, 999);", StringComparison.Ordinal)];

        Assert.Equal(pattern, JsonSerializer.Deserialize<string>(literal));
    }

    [Theory]
    [InlineData("findNextMatch")]
    [InlineData("findPreviousMatch")]
    public void BuildFindMatchScript_PassesArgumentsInMonacoOrder(string method)
    {
        var script = ModelHelper.BuildFindMatchScript(
            method,
            searchString: "needle",
            searchStart: new Position(3, 5),
            isRegex: false,
            matchCase: true,
            wordSeparators: null,
            captureMatches: true);

        Assert.Equal(
            $"EditorContext.getEditorForElement(element).model.{method}(\"needle\", {{\"column\":5,\"lineNumber\":3}}, false, true, null, true);",
            script);
    }
}
