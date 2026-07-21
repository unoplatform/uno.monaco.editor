using Monaco;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Tests for <see cref="UriHelper.AbsoluteUriString"/> static method.
/// On desktop (non-browser), the method returns the absolute URI string directly
/// without Wasm bootstrap path manipulation.
/// </summary>
public sealed class UriHelperTests
{
    [Fact]
    public void AbsoluteUri_HttpsScheme_ReturnsAbsoluteUri()
    {
        var uri = new global::System.Uri("https://example.com/page");
        var result = uri.AbsoluteUriString();
        Assert.Equal("https://example.com/page", result);
    }

    [Fact]
    public void AbsoluteUri_FileScheme_NonBrowser_ReturnsAbsoluteUri()
    {
        // On non-browser (desktop), file:// URIs return AbsoluteUri directly
        // because OperatingSystem.IsBrowser() returns false.
        var uri = new global::System.Uri("file:///path/to/file.html");
        var result = uri.AbsoluteUriString();
        Assert.Equal("file:///path/to/file.html", result);
    }

    [Fact]
    public void AbsoluteUri_MsAppxWeb_NonBrowser_ReturnsAbsoluteUri()
    {
        // On non-browser, ms-appx-web:// returns AbsoluteUri directly.
        var uri = new global::System.Uri("ms-appx-web:///Resources/editor.html");
        var result = uri.AbsoluteUriString();
        Assert.StartsWith("ms-appx-web:", result);
    }

    [Fact]
    public void AbsoluteUri_PreservesQueryString()
    {
        var uri = new global::System.Uri("https://example.com/page?key=value&other=123");
        var result = uri.AbsoluteUriString();
        Assert.Contains("key=value", result);
        Assert.Contains("other=123", result);
    }

    [Fact]
    public void AbsoluteUri_PreservesFragment()
    {
        var uri = new global::System.Uri("https://example.com/page#section");
        var result = uri.AbsoluteUriString();
        Assert.Contains("#section", result);
    }

    [Fact]
    public void RelativeUri_NonBrowser_ReturnsOriginalString()
    {
        // Relative URIs use OriginalString. On non-browser with no env vars set,
        // fallback behavior returns OriginalString.
        var uri = new global::System.Uri("relative/path.html", UriKind.Relative);
        var result = uri.AbsoluteUriString();
        Assert.Contains("relative/path.html", result);
    }
}
