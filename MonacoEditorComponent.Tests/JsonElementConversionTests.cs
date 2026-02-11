using System.Text.Json;

using Monaco.Helpers;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Tests for the deterministic JsonElement to string[] conversion rules
/// in <see cref="ParentAccessorDesktop"/>.
/// </summary>
public sealed class JsonElementConversionTests
{
    // ============================================================
    // ConvertJsonElementToStringArray tests
    // ============================================================

    [Fact]
    public void Array_ElementWiseGetRawText()
    {
        using var doc = JsonDocument.Parse("""["a","b"]""");
        var result = ParentAccessorDesktop.ConvertJsonElementToStringArray(doc.RootElement);

        Assert.Equal(2, result.Length);
        Assert.Equal("\"a\"", result[0]);
        Assert.Equal("\"b\"", result[1]);
    }

    [Fact]
    public void String_SingleElement()
    {
        using var doc = JsonDocument.Parse("\"single\"");
        var result = ParentAccessorDesktop.ConvertJsonElementToStringArray(doc.RootElement);

        Assert.Single(result);
        Assert.Equal("single", result[0]);
    }

    [Fact]
    public void Null_EmptyArray()
    {
        using var doc = JsonDocument.Parse("null");
        var result = ParentAccessorDesktop.ConvertJsonElementToStringArray(doc.RootElement);

        Assert.Empty(result);
    }

    [Fact]
    public void Undefined_EmptyArray()
    {
        // JsonValueKind.Undefined is the default for a default(JsonElement).
        var element = default(JsonElement);
        var result = ParentAccessorDesktop.ConvertJsonElementToStringArray(element);

        Assert.Empty(result);
    }

    [Fact]
    public void Object_SingleElementRawText()
    {
        using var doc = JsonDocument.Parse("""{"key":"val"}""");
        var result = ParentAccessorDesktop.ConvertJsonElementToStringArray(doc.RootElement);

        Assert.Single(result);
        Assert.Equal("""{"key":"val"}""", result[0]);
    }

    [Fact]
    public void Number_SingleElementRawText()
    {
        using var doc = JsonDocument.Parse("42");
        var result = ParentAccessorDesktop.ConvertJsonElementToStringArray(doc.RootElement);

        Assert.Single(result);
        Assert.Equal("42", result[0]);
    }

    [Fact]
    public void Boolean_SingleElementRawText()
    {
        using var doc = JsonDocument.Parse("true");
        var result = ParentAccessorDesktop.ConvertJsonElementToStringArray(doc.RootElement);

        Assert.Single(result);
        Assert.Equal("true", result[0]);
    }

    [Fact]
    public void NestedArray_PreservesJsonTokenFidelity()
    {
        using var doc = JsonDocument.Parse("""[1,"two",[3,4],{"five":5}]""");
        var result = ParentAccessorDesktop.ConvertJsonElementToStringArray(doc.RootElement);

        Assert.Equal(4, result.Length);
        Assert.Equal("1", result[0]);
        Assert.Equal("\"two\"", result[1]);
        Assert.Equal("[3,4]", result[2]);
        Assert.Equal("""{"five":5}""", result[3]);
    }

    [Fact]
    public void EmptyArray_ReturnsEmptyStringArray()
    {
        using var doc = JsonDocument.Parse("[]");
        var result = ParentAccessorDesktop.ConvertJsonElementToStringArray(doc.RootElement);

        Assert.Empty(result);
    }

    [Fact]
    public void EmptyString_SingleElement()
    {
        using var doc = JsonDocument.Parse("\"\"");
        var result = ParentAccessorDesktop.ConvertJsonElementToStringArray(doc.RootElement);

        Assert.Single(result);
        Assert.Equal(string.Empty, result[0]);
    }

    // ============================================================
    // ExtractStringValue tests
    // ============================================================

    [Fact]
    public void ExtractString_StringValue()
    {
        using var doc = JsonDocument.Parse("\"hello\"");
        var result = ParentAccessorDesktop.ExtractStringValue(doc.RootElement);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ExtractString_NullValue()
    {
        using var doc = JsonDocument.Parse("null");
        var result = ParentAccessorDesktop.ExtractStringValue(doc.RootElement);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractString_UndefinedValue()
    {
        var result = ParentAccessorDesktop.ExtractStringValue(default);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractString_NumberValue_ReturnsRawText()
    {
        using var doc = JsonDocument.Parse("42");
        var result = ParentAccessorDesktop.ExtractStringValue(doc.RootElement);
        Assert.Equal("42", result);
    }

    [Fact]
    public void ExtractString_ObjectValue_ReturnsRawText()
    {
        using var doc = JsonDocument.Parse("""{"key":"val"}""");
        var result = ParentAccessorDesktop.ExtractStringValue(doc.RootElement);
        Assert.Equal("""{"key":"val"}""", result);
    }

    [Fact]
    public void ExtractString_EmptyString()
    {
        using var doc = JsonDocument.Parse("\"\"");
        var result = ParentAccessorDesktop.ExtractStringValue(doc.RootElement);
        Assert.Equal(string.Empty, result);
    }
}
