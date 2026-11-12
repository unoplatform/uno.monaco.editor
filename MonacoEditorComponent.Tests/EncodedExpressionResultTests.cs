using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.Playwright;

using Monaco.Extensions;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Pins <see cref="ICodeEditorPresenterExtensions.RunEncodedExpressionAsync{T}"/>: text read
/// back from the page arrives intact whichever way the WebView hands the result over.
/// </summary>
/// <remarks>
/// WebView2 on Windows and <c>InvokeJS</c> on WASM return a valid JSON string literal. The macOS
/// WebView escapes only <c>"</c> and leaves line breaks and backslashes raw
/// (unoplatform/uno#26294), which is what made <c>GetValueAsync</c> return null there for any
/// document with more than one line.
/// </remarks>
public sealed class EncodedExpressionResultTests
{
    private static readonly JsonSerializerOptions JsStringifyOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Dictionary<string, string> Texts = new()
    {
        ["line breaks"] = "line 1\nline 2\r\nline 3",
        ["backslashes"] = @"C:\temp\new",
        ["tab"] = "\tindented",
        ["quotes and percent"] = "say \"100%\" and %22 and %5C",
        ["control characters"] = "a\u0000b\u001fc",
        ["non-ASCII"] = "caf\u00e9 \u2028 \U0001F600",
        ["escape-looking text"] = @"a literal \n and \u0041",
    };

    public static TheoryData<string, Bridge> TextCases()
    {
        var data = new TheoryData<string, Bridge>();
        foreach (var name in Texts.Keys)
        {
            data.Add(name, Bridge.Json);
            data.Add(name, Bridge.MacOs);
        }

        return data;
    }

    public enum Bridge
    {
        /// <summary>WebView2 on Windows and InvokeJS on WASM: a valid JSON string literal.</summary>
        Json,

        /// <summary>macOS WebView: only <c>"</c> escaped, everything else raw.</summary>
        MacOs,
    }

    private readonly MockCodeEditorPresenter _presenter = new();

    [Fact]
    public async Task RunScriptAsync_MacOsMultiLineString_Fails()
    {
        // The bug this guards against: the plain path cannot read a macOS multi-line result.
        _presenter.ScriptWithElementResult = HandOver("line 1\nline 2", Bridge.MacOs);

        await Assert.ThrowsAsync<JavaScriptExecutionException>(
            () => _presenter.RunScriptAsync<string>("model.getValue();"));
    }

    [Theory]
    [MemberData(nameof(TextCases))]
    public async Task RunEncodedExpressionAsync_ReturnsTextIntact(string textName, Bridge bridge)
    {
        var text = Texts[textName];
        _presenter.ScriptWithElementResult = HandOver(EncodeAsPage(text), bridge);

        var result = await _presenter.RunEncodedExpressionAsync<string>("model.getValue()");

        Assert.Equal(text, result);
    }

    [Theory]
    [InlineData(Bridge.Json)]
    [InlineData(Bridge.MacOs)]
    public async Task RunEncodedExpressionAsync_ReturnsLinesIntact(Bridge bridge)
    {
        string[] lines = [@"var path = ""C:\temp"";", "\tindented", "\"quoted\""];
        _presenter.ScriptWithElementResult = HandOver(EncodeAsPage(lines), bridge);

        var result = await _presenter.RunEncodedExpressionAsync<IEnumerable<string>>("model.getLinesContent()");

        Assert.Equal(lines, result);
    }

    [Theory]
    [InlineData("null")] // a null result, WebView2 and macOS
    [InlineData("\"\"")] // a null result on WASM, which coerces a falsy result to ""
    [InlineData("")]
    public async Task RunEncodedExpressionAsync_NullResult_ReturnsNull(string raw)
    {
        _presenter.ScriptWithElementResult = raw;

        Assert.Null(await _presenter.RunEncodedExpressionAsync<string>("model.getValue()"));
    }

    [Theory]
    [InlineData(Bridge.Json)]
    [InlineData(Bridge.MacOs)]
    public async Task RunEncodedExpressionAsync_EmptyString_ReturnsNull(Bridge bridge)
    {
        // Same as the plain path, which has always read an empty string as null.
        _presenter.ScriptWithElementResult = HandOver(EncodeAsPage(string.Empty), bridge);

        Assert.Null(await _presenter.RunEncodedExpressionAsync<string>("model.getValue()"));
    }

    [Fact]
    public async Task RunEncodedExpressionAsync_WrapsTheExpression()
    {
        await _presenter.RunEncodedExpressionAsync<string>("EditorContext.getEditorForElement(element).model.getValue()");

        var script = Assert.Single(_presenter.InvokeScriptWithElementCalls);
        Assert.Equal(
            ICodeEditorPresenterExtensions.EncodeExpressionResult("EditorContext.getEditorForElement(element).model.getValue()"),
            script);
        Assert.Empty(_presenter.InvokeMethodCalls);
        Assert.Empty(_presenter.InvokeScriptCalls);
    }

    [Fact]
    [Trait("Category", "BrowserScript")]
    public async Task EncodeExpressionResult_InChromium_SurvivesTheMacOsBridge()
    {
        // Runs the real wrapper script, so the JavaScript half is tested and not only its C# mirror.
        PlaywrightDriverPath.Ensure();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();

        foreach (var (name, text) in Texts)
        {
            var jsLiteral = JsonSerializer.Serialize(text);
            var pageResult = await page.EvaluateAsync<string>(ICodeEditorPresenterExtensions.EncodeExpressionResult(jsLiteral));

            var presenter = new MockCodeEditorPresenter { ScriptWithElementResult = HandOver(pageResult, Bridge.MacOs) };
            var result = await presenter.RunEncodedExpressionAsync<string>("unused");

            Assert.True(text == result, $"'{name}' did not survive the round trip.");
        }

        Assert.Null(await page.EvaluateAsync<string?>(ICodeEditorPresenterExtensions.EncodeExpressionResult("undefined")));
        Assert.Equal("null", await page.EvaluateAsync<string>(ICodeEditorPresenterExtensions.EncodeExpressionResult("null")));
    }

    /// <summary>What the page returns for <paramref name="value"/>: its JSON, with <c>%</c>, <c>"</c> and <c>\</c> percent-encoded.</summary>
    private static string EncodeAsPage<T>(T value)
        => JsonSerializer.Serialize(value, JsStringifyOptions)
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\"", "%22", StringComparison.Ordinal)
            .Replace("\\", "%5C", StringComparison.Ordinal);

    /// <summary>The raw string the presenter receives for a string <paramref name="pageResult"/>.</summary>
    private static string HandOver(string pageResult, Bridge bridge)
        => bridge == Bridge.Json
            ? JsonSerializer.Serialize(pageResult)
            : "\"" + pageResult.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
