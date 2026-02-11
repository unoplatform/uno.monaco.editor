using Monaco;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Tests for <see cref="DesktopCodeEditorPresenter.IsNavigationAllowed"/>.
/// Validates the navigation allowlist for about:blank, virtual host HTTPS,
/// file:// with content root, and blocked external URIs.
/// </summary>
public sealed class IsNavigationAllowedTests
{
    [Fact]
    public void AboutBlank_Allowed()
    {
        Assert.True(DesktopCodeEditorPresenter.IsNavigationAllowed("about:blank", null));
    }

    [Fact]
    public void AboutBlankWithFragment_Allowed()
    {
        Assert.True(DesktopCodeEditorPresenter.IsNavigationAllowed("about:blank#fragment", null));
    }

    [Fact]
    public void AboutBlankEvil_Blocked()
    {
        // URI-parsed, not prefix-matched: about:blankevil is a different path.
        Assert.False(DesktopCodeEditorPresenter.IsNavigationAllowed("about:blankevil", null));
    }

    [Fact]
    public void AllowedVirtualHost_Https_Allowed()
    {
        var uri = $"https://{DesktopCodeEditorPresenter.AllowedVirtualHost}/editor.html";
        Assert.True(DesktopCodeEditorPresenter.IsNavigationAllowed(uri, null));
    }

    [Fact]
    public void AllowedVirtualHost_WithPath_Allowed()
    {
        var uri = $"https://{DesktopCodeEditorPresenter.AllowedVirtualHost}/subdir/page.html";
        Assert.True(DesktopCodeEditorPresenter.IsNavigationAllowed(uri, null));
    }

    [Fact]
    public void ExternalHttps_Blocked()
    {
        Assert.False(DesktopCodeEditorPresenter.IsNavigationAllowed("https://evil.com/malware", null));
    }

    [Fact]
    public void AllowedVirtualHost_NonDefaultPort_Blocked()
    {
        var uri = $"https://{DesktopCodeEditorPresenter.AllowedVirtualHost}:8080/editor.html";
        Assert.False(DesktopCodeEditorPresenter.IsNavigationAllowed(uri, null));
    }

    [Fact]
    public void Http_Blocked()
    {
        Assert.False(DesktopCodeEditorPresenter.IsNavigationAllowed("http://example.com", null));
    }

    [Fact]
    public void FileUri_NullContentRoot_Blocked()
    {
        Assert.False(DesktopCodeEditorPresenter.IsNavigationAllowed("file:///path/to/content/foo.html", null));
    }

    [Fact]
    public void FileUri_EmptyContentRoot_Blocked()
    {
        Assert.False(DesktopCodeEditorPresenter.IsNavigationAllowed("file:///path/to/content/foo.html", ""));
    }

    [Fact]
    public void FileUri_UnderContentRoot_Allowed()
    {
        // Use a temp directory that actually exists to ensure Path.GetFullPath works correctly.
        var root = Path.Combine(Path.GetTempPath(), "test-content");
        var uri = $"file://{root}/foo.html";
        Assert.True(DesktopCodeEditorPresenter.IsNavigationAllowed(uri, root));
    }

    [Fact]
    public void FileUri_OutsideContentRoot_Blocked()
    {
        var root = Path.Combine(Path.GetTempPath(), "test-content");
        var uri = $"file://{Path.Combine(Path.GetTempPath(), "other-dir")}/evil.html";
        Assert.False(DesktopCodeEditorPresenter.IsNavigationAllowed(uri, root));
    }

    [Fact]
    public void FileUri_PathTraversal_Blocked()
    {
        // Path traversal attack: file:///path/to/content/../secret
        // Path.GetFullPath canonicalizes ".." so it resolves outside the root.
        var root = Path.Combine(Path.GetTempPath(), "test-content");
        var uri = $"file://{root}/../secret";
        Assert.False(DesktopCodeEditorPresenter.IsNavigationAllowed(uri, root));
    }

    [Fact]
    public void InvalidUri_Blocked()
    {
        Assert.False(DesktopCodeEditorPresenter.IsNavigationAllowed("not-a-valid-uri", null));
    }

    [Fact]
    public void DataUri_Blocked()
    {
        Assert.False(DesktopCodeEditorPresenter.IsNavigationAllowed("data:text/html,<h1>evil</h1>", null));
    }

    [Fact]
    public void JavascriptUri_Blocked()
    {
        Assert.False(DesktopCodeEditorPresenter.IsNavigationAllowed("javascript:alert(1)", null));
    }
}
