using Monaco;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Verifies <see cref="RenderingBackend"/> enum values.
/// </summary>
public sealed class RenderingBackendTests
{
    [Fact]
    public void Wasm_HasExpectedValue()
    {
        Assert.Equal(0, (int)RenderingBackend.Wasm);
    }

    [Fact]
    public void Desktop_HasExpectedValue()
    {
        Assert.Equal(1, (int)RenderingBackend.Desktop);
    }

    [Fact]
    public void EnumHasExactlyTwoValues()
    {
        var values = Enum.GetValues<RenderingBackend>();
        Assert.Equal(2, values.Length);
    }

    [Fact]
    public void CanParseFromString()
    {
        Assert.Equal(RenderingBackend.Wasm, Enum.Parse<RenderingBackend>("Wasm"));
        Assert.Equal(RenderingBackend.Desktop, Enum.Parse<RenderingBackend>("Desktop"));
    }
}
