using Monaco;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Tests for <see cref="DesktopCodeEditorPresenter.TryResolveDesktopContentPath"/> and
/// <see cref="DesktopCodeEditorPresenter.GetDesktopContentCandidates"/>.
/// Validates the two-candidate probe that locates the desktop content root:
/// <c>&lt;base&gt;/DesktopContent</c> (ProjectReference output) and
/// <c>&lt;base&gt;/&lt;AssemblyName&gt;/DesktopContent</c> (NuGet package output, produced by
/// Uno's <c>GenerateLibraryLayout=true</c> asset packing).
/// </summary>
public sealed class TryResolveDesktopContentPathTests : IDisposable
{
    /// <summary>The assembly-name prefix Uno uses for library-layout assets.</summary>
    private const string LibraryFolder = "MonacoEditorComponent";

    private const string ContentFolder = "DesktopContent";

    private readonly string _baseDirectory;

    public TryResolveDesktopContentPathTests()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), $"monaco-content-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_baseDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(_baseDirectory, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    /// <summary>
    /// Creates a candidate folder under the temp base directory, optionally writing
    /// <c>editor.html</c> into it. Returns the created folder path.
    /// </summary>
    private string CreateContentFolder(string relativePath, bool withEditorHtml)
    {
        var folder = Path.Combine(_baseDirectory, relativePath);
        Directory.CreateDirectory(folder);
        if (withEditorHtml)
        {
            File.WriteAllText(Path.Combine(folder, "editor.html"), "<html></html>");
        }

        return folder;
    }

    [Fact]
    public void RootOnlyLayout_ReturnsRootFolder()
    {
        var expected = CreateContentFolder(ContentFolder, withEditorHtml: true);

        Assert.Equal(expected, DesktopCodeEditorPresenter.TryResolveDesktopContentPath(
            baseDirectory: _baseDirectory,
            libraryLayoutFolderName: LibraryFolder));
    }

    [Fact]
    public void LibraryLayoutOnly_ReturnsNestedFolder()
    {
        // The NuGet package deploys content ONLY here (GenerateLibraryLayout=true).
        var expected = CreateContentFolder(Path.Combine(LibraryFolder, ContentFolder), withEditorHtml: true);

        Assert.Equal(expected, DesktopCodeEditorPresenter.TryResolveDesktopContentPath(
            baseDirectory: _baseDirectory,
            libraryLayoutFolderName: LibraryFolder));
    }

    [Fact]
    public void BothLayoutsPresent_PrefersRootFolder()
    {
        // A ProjectReference build produces BOTH layouts from the same source files.
        // Root-first keeps that existing resolution unchanged.
        var root = CreateContentFolder(ContentFolder, withEditorHtml: true);
        CreateContentFolder(Path.Combine(LibraryFolder, ContentFolder), withEditorHtml: true);

        Assert.Equal(root, DesktopCodeEditorPresenter.TryResolveDesktopContentPath(
            baseDirectory: _baseDirectory,
            libraryLayoutFolderName: LibraryFolder));
    }

    [Fact]
    public void NeitherLayoutPresent_ReturnsNull()
    {
        Assert.Null(DesktopCodeEditorPresenter.TryResolveDesktopContentPath(
            baseDirectory: _baseDirectory,
            libraryLayoutFolderName: LibraryFolder));
    }

    [Fact]
    public void RootFolderWithoutEditorHtml_FallsBackToLibraryLayout()
    {
        // A stale or empty DesktopContent must not win: it would produce a blank WebView2.
        CreateContentFolder(ContentFolder, withEditorHtml: false);
        var expected = CreateContentFolder(Path.Combine(LibraryFolder, ContentFolder), withEditorHtml: true);

        Assert.Equal(expected, DesktopCodeEditorPresenter.TryResolveDesktopContentPath(
            baseDirectory: _baseDirectory,
            libraryLayoutFolderName: LibraryFolder));
    }

    [Fact]
    public void AllFoldersWithoutEditorHtml_ReturnsNull()
    {
        // Directory.Exists would have selected one of these; File.Exists must not.
        CreateContentFolder(ContentFolder, withEditorHtml: false);
        CreateContentFolder(Path.Combine(LibraryFolder, ContentFolder), withEditorHtml: false);

        Assert.Null(DesktopCodeEditorPresenter.TryResolveDesktopContentPath(
            baseDirectory: _baseDirectory,
            libraryLayoutFolderName: LibraryFolder));
    }

    [Fact]
    public void RenamedLibraryFolder_ProbesSuppliedFolderName()
    {
        // The library-layout prefix is Uno's $(AssemblyName), so the probe must use the
        // supplied name rather than a hardcoded "MonacoEditorComponent".
        const string renamed = "SomeRenamedAssembly";
        var expected = CreateContentFolder(Path.Combine(renamed, ContentFolder), withEditorHtml: true);

        Assert.Equal(expected, DesktopCodeEditorPresenter.TryResolveDesktopContentPath(
            baseDirectory: _baseDirectory,
            libraryLayoutFolderName: renamed));

        Assert.Null(DesktopCodeEditorPresenter.TryResolveDesktopContentPath(
            baseDirectory: _baseDirectory,
            libraryLayoutFolderName: LibraryFolder));
    }

    [Fact]
    public void GetDesktopContentCandidates_RootBeforeLibraryLayout()
    {
        var candidates = DesktopCodeEditorPresenter.GetDesktopContentCandidates(
            baseDirectory: _baseDirectory,
            libraryLayoutFolderName: LibraryFolder);

        Assert.Equal(
            [
                Path.Combine(_baseDirectory, ContentFolder),
                Path.Combine(_baseDirectory, LibraryFolder, ContentFolder),
            ],
            candidates);
    }

    /// <summary>
    /// Pins the contract that Uno's library-layout prefix is <c>$(AssemblyName)</c>: if the
    /// assembly is ever renamed, the derived probe folder must follow it.
    /// </summary>
    [Fact]
    public void AssemblyName_MatchesLibraryLayoutFolderName()
    {
        Assert.Equal(LibraryFolder, typeof(DesktopCodeEditorPresenter).Assembly.GetName().Name);
    }

    /// <summary>
    /// The resolved root is assigned to <c>AllowedFileContentRoot</c>, so it must gate
    /// <see cref="DesktopCodeEditorPresenter.IsNavigationAllowed"/> for the library layout too.
    /// </summary>
    [Fact]
    public void LibraryLayoutRoot_GatesFileNavigation()
    {
        var root = CreateContentFolder(Path.Combine(LibraryFolder, ContentFolder), withEditorHtml: true);

        Assert.True(DesktopCodeEditorPresenter.IsNavigationAllowed(
            DesktopCodeEditorPresenter.BuildFileEditorUri(root).ToString(), root));
    }
}
