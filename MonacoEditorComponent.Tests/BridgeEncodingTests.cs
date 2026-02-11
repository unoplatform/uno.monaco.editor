using Monaco.Helpers;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Tests for the <see cref="BridgeEncoding"/> sanitize/desanitize helper.
/// Validates round-trip encoding of special characters through the WASM bridge,
/// including the critical '%' self-encoding edge case.
/// </summary>
public sealed class BridgeEncodingTests
{
    [Fact]
    public void Sanitize_NullInput_ReturnsNull()
    {
        Assert.Null(BridgeEncoding.Sanitize(null));
    }

    [Fact]
    public void Desanitize_NullInput_ReturnsNull()
    {
        Assert.Null(BridgeEncoding.Desanitize(null));
    }

    [Fact]
    public void Sanitize_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, BridgeEncoding.Sanitize(string.Empty));
    }

    [Fact]
    public void Desanitize_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, BridgeEncoding.Desanitize(string.Empty));
    }

    [Fact]
    public void Sanitize_NoSpecialChars_ReturnsUnchanged()
    {
        const string input = "hello world 123";
        Assert.Equal(input, BridgeEncoding.Sanitize(input));
    }

    [Fact]
    public void Sanitize_EncodesAmpersand()
    {
        var result = BridgeEncoding.Sanitize("a&b");
        Assert.DoesNotContain("&", result);
        Assert.Contains("%" + (int)'&', result);
    }

    [Fact]
    public void Sanitize_EncodesColon()
    {
        var result = BridgeEncoding.Sanitize("key:value");
        Assert.DoesNotContain(":", result);
    }

    [Fact]
    public void Sanitize_EncodesCurlyBraces()
    {
        var result = BridgeEncoding.Sanitize("{\"key\":\"value\"}");
        Assert.DoesNotContain("{", result);
        Assert.DoesNotContain("}", result);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData("simple text with spaces")]
    [InlineData("123456")]
    public void RoundTrip_PlainStrings_Survive(string input)
    {
        var encoded = BridgeEncoding.Sanitize(input);
        var decoded = BridgeEncoding.Desanitize(encoded);
        Assert.Equal(input, decoded);
    }

    [Theory]
    [InlineData("{\"name\":\"value\"}")]
    [InlineData("key:value")]
    [InlineData("a&b")]
    [InlineData("it's")]
    [InlineData("a,b,c")]
    [InlineData("path\\to\\file")]
    [InlineData("he said \"hello\"")]
    public void RoundTrip_SpecialChars_Survive(string input)
    {
        var encoded = BridgeEncoding.Sanitize(input);
        var decoded = BridgeEncoding.Desanitize(encoded);
        Assert.Equal(input, decoded);
    }

    /// <summary>
    /// Critical edge case: '%' appears as both an encoded character AND the escape prefix.
    /// Sanitize must encode '%' FIRST (before other replacements) to prevent double-encoding.
    /// Desanitize must decode '%' LAST to prevent premature unescaping.
    /// </summary>
    [Fact]
    public void RoundTrip_PercentSign_SurvivesWithoutDoubleEncoding()
    {
        const string input = "100%";
        var encoded = BridgeEncoding.Sanitize(input);
        var decoded = BridgeEncoding.Desanitize(encoded);
        Assert.Equal(input, decoded);
    }

    [Fact]
    public void RoundTrip_PercentWithOtherSpecialChars_Survives()
    {
        const string input = "100% of {items} & 50% of \"things\"";
        var encoded = BridgeEncoding.Sanitize(input);
        var decoded = BridgeEncoding.Desanitize(encoded);
        Assert.Equal(input, decoded);
    }

    [Fact]
    public void RoundTrip_OnlyPercent_Survives()
    {
        const string input = "%";
        var encoded = BridgeEncoding.Sanitize(input);
        var decoded = BridgeEncoding.Desanitize(encoded);
        Assert.Equal(input, decoded);
    }

    [Fact]
    public void RoundTrip_MultiplePercents_Survive()
    {
        const string input = "%%% percent %%%";
        var encoded = BridgeEncoding.Sanitize(input);
        var decoded = BridgeEncoding.Desanitize(encoded);
        Assert.Equal(input, decoded);
    }

    [Fact]
    public void RoundTrip_PercentFollowedByDigits_Survives()
    {
        // This tests that a string like "%37" (which looks like an encoded '%')
        // round-trips correctly without confusion.
        const string input = "%37";
        var encoded = BridgeEncoding.Sanitize(input);
        var decoded = BridgeEncoding.Desanitize(encoded);
        Assert.Equal(input, decoded);
    }

    [Fact]
    public void RoundTrip_ComplexJsonPayload_Survives()
    {
        const string input = """{"items":[{"name":"test","value":"100%"}],"count":1}""";
        var encoded = BridgeEncoding.Sanitize(input);
        var decoded = BridgeEncoding.Desanitize(encoded);
        Assert.Equal(input, decoded);
    }

    [Fact]
    public void Sanitize_AllSpecialChars_AllEncoded()
    {
        // All characters in the replacement set: % & \ " ' { } : ,
        const string input = @"%&\""'{}:,";
        var encoded = BridgeEncoding.Sanitize(input);

        // After encoding, none of the original special chars should remain as literals
        // (except percent signs that are part of the encoding itself)
        Assert.DoesNotContain("&", encoded);
        Assert.DoesNotContain("\\", encoded);
        Assert.DoesNotContain("'", encoded);
        Assert.DoesNotContain("{", encoded);
        Assert.DoesNotContain("}", encoded);
        Assert.DoesNotContain(":", encoded);
        Assert.DoesNotContain(",", encoded);
    }
}
