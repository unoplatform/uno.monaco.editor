using Monaco;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Tests for <see cref="LanguagesHelper.LanguageIdFromExtensionDesktop"/>, the C#-side
/// extension-to-language mapping used on desktop where the JSImport path is unavailable.
/// <para>On WASM the equivalent lookup runs in JavaScript against Monaco's own language
/// registry, so entries here exist to keep the two platforms in agreement.</para>
/// </summary>
public sealed class LanguagesHelperExtensionMapTests
{
    /// <summary>
    /// <c>diff</c> is registered by the component (see
    /// <c>ts-helpermethods/languages/diff.ts</c>) rather than by Monaco, which ships no
    /// diff grammar. The desktop table has to carry it explicitly, since it cannot read
    /// Monaco's registry the way the WASM path does.
    /// </summary>
    [Theory]
    [InlineData("diff")]
    [InlineData(".diff")]
    [InlineData("patch")]
    [InlineData(".patch")]
    [InlineData("changes.diff")]
    [InlineData("0001-fix-thing.patch")]
    [InlineData("artifacts/review/changes.DIFF")]
    public void DiffExtensions_MapToDiff(string extension)
    {
        Assert.Equal("diff", LanguagesHelper.LanguageIdFromExtensionDesktop(extension));
    }

    [Theory]
    [InlineData("cs", "csharp")]
    [InlineData(".csproj", "xml")]
    [InlineData("Program.cs", "csharp")]
    [InlineData("md", "markdown")]
    public void KnownExtensions_MapToExpectedLanguage(string extension, string expected)
    {
        Assert.Equal(expected, LanguagesHelper.LanguageIdFromExtensionDesktop(extension));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(".unmapped")]
    [InlineData("notes.unmapped")]
    public void UnmappedOrEmptyExtensions_FallBackToPlaintext(string? extension)
    {
        Assert.Equal("plaintext", LanguagesHelper.LanguageIdFromExtensionDesktop(extension));
    }
}
