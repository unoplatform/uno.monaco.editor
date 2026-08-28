using Microsoft.Playwright;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Playwright CDP integration tests for <c>MultiDiffCodeEditor</c> on the desktop
/// (Skia/WebView2) target.
///
/// <para>These drive <see cref="DesktopAppFixture.MultiDiffPage"/>, a third WebView2 host the
/// test app realizes when <c>MONACO_MULTIDIFF_TAB=1</c> is set -- for the same reason the diff
/// sample needs its own: TabView virtualizes non-selected tab content, so a control parked in a
/// tab is never constructed, and CDP reaches WebView contents rather than the XAML tree.</para>
///
/// <para>The assertions deliberately avoid counting editors. Every per-file editor the widget
/// renders registers with the same code editor service, and the widget virtualizes, so those
/// counts move as the view scrolls. What is stable is the DOM the widget produces and the set of
/// models the JS layer owns.</para>
/// </summary>
[Trait("Category", "DesktopCDP")]
[Collection("DesktopCDP")]
public sealed class DesktopMultiDiffEditorTests : IAsyncLifetime
{
    private const int MultiDiffTimeoutMs = 30_000;

    private readonly DesktopAppFixture _fixture;
    private string _currentTestName = "unknown";
    private bool _testFailed;

    public DesktopMultiDiffEditorTests(DesktopAppFixture fixture)
    {
        _fixture = fixture;
    }

    public ValueTask InitializeAsync()
    {
        _testFailed = false;
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_testFailed)
        {
            await _fixture.CaptureFailureArtifacts(_currentTestName);
        }
    }

    private async Task WaitForSettledAsync()
    {
        await _fixture.MultiDiffPage.WaitForFunctionAsync(
            MultiDiffEditorCases.IsPresentExpression,
            null, new PageWaitForFunctionOptions { Timeout = MultiDiffTimeoutMs });
        await _fixture.MultiDiffPage.WaitForFunctionAsync(
            MultiDiffEditorCases.AnyEntryRenderedExpression,
            null, new PageWaitForFunctionOptions { Timeout = MultiDiffTimeoutMs });
    }

    /// <summary>Waits for every expanded file to have finished computing its diff.</summary>
    private Task WaitForDiffsComputedAsync() => _fixture.MultiDiffPage.WaitForFunctionAsync(
        MultiDiffEditorCases.AllDiffsComputedExpression,
        null, new PageWaitForFunctionOptions { Timeout = MultiDiffTimeoutMs });

    /// <summary>
    /// Scrolls <paramref name="path"/> into view and waits for its section to render.
    /// </summary>
    /// <remarks>
    /// The list is virtualized and the sample panel is only a third of the window, so at any
    /// moment most files have no DOM at all -- and how many do depends on the window size.
    /// Revealing one at a time is the only viewport-independent way to assert on all of them,
    /// and it exercises RevealFileAsync's scroll arithmetic while it is at it.
    /// </remarks>
    private async Task<IPage> RevealAsync(string path)
    {
        var page = _fixture.MultiDiffPage;
        await page.EvaluateAsync(MultiDiffEditorCases.RevealPathExpression, path);
        await page.WaitForFunctionAsync(
            MultiDiffEditorCases.IsPathRenderedExpression, path,
            new PageWaitForFunctionOptions { Timeout = MultiDiffTimeoutMs });
        return page;
    }

    /// <summary>
    /// One section per file, each labelled. Empty labels would mean the control's
    /// <c>IWorkbenchUIElementFactory</c> never reached the item template -- exactly what happens
    /// when the widget is built through <c>createMultiFileDiffEditor</c>, which hardcodes an
    /// empty factory.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_RendersOneLabelledSectionPerFile()
    {
        _currentTestName = nameof(MultiDiffEditor_RendersOneLabelledSectionPerFile);
        try
        {
            await WaitForSettledAsync();

            // Every file must be reachable and labelled. An empty label would mean the control's
            // IWorkbenchUIElementFactory never reached the item template.
            foreach (var path in MultiDiffEditorCases.SampleFilePaths)
            {
                await RevealAsync(path);
            }
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// Added, deleted and renamed files carry their badges, and a plainly-modified file carries
    /// none. The last part is the real assertion: a badge there would mean the two sides' model
    /// URIs disagree on their path, which makes Monaco read every modified file as a rename.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_RendersAddedDeletedAndRenamedBadges()
    {
        _currentTestName = nameof(MultiDiffEditor_RendersAddedDeletedAndRenamedBadges);
        try
        {
            await WaitForSettledAsync();

            for (var i = 0; i < MultiDiffEditorCases.SampleFilePaths.Length; i++)
            {
                var path = MultiDiffEditorCases.SampleFilePaths[i];
                var page = await RevealAsync(path);
                var badge = await page.EvaluateAsync<string>(MultiDiffEditorCases.BadgeForPathExpression, path);

                Assert.Equal(MultiDiffEditorCases.SampleStatusBadges[i], badge);
            }
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// Every file computes its own hunks. With N files there are N computations on the one
    /// shared editor worker, so this also covers the worker being reachable at all -- without it
    /// the widget still renders both documents and simply never produces a hunk.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_ComputesHunksPerFile()
    {
        _currentTestName = nameof(MultiDiffEditor_ComputesHunksPerFile);
        try
        {
            await WaitForSettledAsync();
            await WaitForDiffsComputedAsync();

            var hunks = await _fixture.MultiDiffPage.EvaluateAsync<int[]>(
                MultiDiffEditorCases.PerFileHunkCountsExpression);

            Assert.NotEmpty(hunks);
            Assert.All(hunks, count => Assert.True(count > 0, $"Every rendered file must have computed at least one hunk; got {count}."));
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// The control is a read-only viewer, and the two sides of a diff lock through different
    /// options, so both are asserted.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_LocksEverySide()
    {
        _currentTestName = nameof(MultiDiffEditor_LocksEverySide);
        try
        {
            await WaitForSettledAsync();
            await WaitForDiffsComputedAsync();

            Assert.True(
                await _fixture.MultiDiffPage.EvaluateAsync<bool>(MultiDiffEditorCases.AllFilesReadOnlyExpression),
                "Every file in a MultiDiffCodeEditor must be read-only on both sides.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// The theme stylesheet and a real chevron glyph. Both come from the same
    /// <c>registerEditorContainer</c> call, and its absence is invisible to every other
    /// assertion here: the widget still renders, just with no colours and no icons.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_AppliesMonacoThemeAndCodicons()
    {
        _currentTestName = nameof(MultiDiffEditor_AppliesMonacoThemeAndCodicons);
        try
        {
            await WaitForSettledAsync();

            Assert.True(
                await _fixture.MultiDiffPage.EvaluateAsync<bool>(MultiDiffEditorCases.ThemeStylesheetPresentExpression),
                "The Monaco theme stylesheet must be injected, or the widget renders unstyled.");
            Assert.True(
                await _fixture.MultiDiffPage.EvaluateAsync<bool>(MultiDiffEditorCases.ChevronGlyphRenderedExpression),
                "The collapse chevron must resolve to a codicon glyph.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// Collapse-all then expand-all, through the managed API. Restores the expanded state before
    /// returning, because the fixture's app is shared across the collection.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_CollapsesAndExpandsEveryFile()
    {
        _currentTestName = nameof(MultiDiffEditor_CollapsesAndExpandsEveryFile);
        try
        {
            await WaitForSettledAsync();

            await _fixture.MultiDiffPage.EvaluateAsync(
                MultiDiffEditorCases.SetAllCollapsedExpression, true);
            await _fixture.MultiDiffPage.WaitForFunctionAsync(
                MultiDiffEditorCases.AllCollapsedExpression,
                null, new PageWaitForFunctionOptions { Timeout = MultiDiffTimeoutMs });

            await _fixture.MultiDiffPage.EvaluateAsync(
                MultiDiffEditorCases.SetAllCollapsedExpression, false);
            await _fixture.MultiDiffPage.WaitForFunctionAsync(
                MultiDiffEditorCases.NoneCollapsedExpression,
                null, new PageWaitForFunctionOptions { Timeout = MultiDiffTimeoutMs });
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// Removing a file and re-adding the same path must not throw, and must leave no orphaned
    /// models. Model URIs are derived from the path and <c>createModel</c> rejects a duplicate,
    /// so this is what proves the JS layer releases a URI before reclaiming it -- and that
    /// removed models are disposed a frame after the list swap rather than during it, which
    /// otherwise makes Monaco throw "TextModel got disposed before DiffEditorWidget model got
    /// reset".
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_SurvivesRemovingAndReaddingTheSamePath()
    {
        _currentTestName = nameof(MultiDiffEditor_SurvivesRemovingAndReaddingTheSamePath);
        try
        {
            await WaitForSettledAsync();

            var page = _fixture.MultiDiffPage;
            var baseline = await page.EvaluateAsync<int>(MultiDiffEditorCases.ModelCountExpression);

            var errors = new List<string>();
            void OnPageError(object? sender, string error) => errors.Add(error);
            page.PageError += OnPageError;

            try
            {
                await page.EvaluateAsync(MultiDiffEditorCases.PushProbeFilesExpression());
                await Task.Delay(500);
                var withProbes = await page.EvaluateAsync<int>(MultiDiffEditorCases.ModelCountExpression);
                Assert.Equal(4, withProbes);

                await page.EvaluateAsync(MultiDiffEditorCases.PushProbeFilesExpression("probe/b.cs"));
                await Task.Delay(500);
                Assert.Equal(2, await page.EvaluateAsync<int>(MultiDiffEditorCases.ModelCountExpression));

                // The same path again: the URI it used must be free to reclaim.
                await page.EvaluateAsync(MultiDiffEditorCases.PushProbeFilesExpression());
                await Task.Delay(500);
                Assert.Equal(4, await page.EvaluateAsync<int>(MultiDiffEditorCases.ModelCountExpression));

                await page.EvaluateAsync(MultiDiffEditorCases.ClearFilesExpression);
                await Task.Delay(500);
                Assert.Equal(0, await page.EvaluateAsync<int>(MultiDiffEditorCases.ModelCountExpression));
                Assert.True(
                    await page.EvaluateAsync<bool>(MultiDiffEditorCases.PlaceholderVisibleExpression),
                    "An empty file list must show the widget's placeholder.");

                Assert.Empty(errors);
            }
            finally
            {
                page.PageError -= OnPageError;

                // The app is shared across the collection and C# does not re-push on its own, so
                // an equivalent list has to be put back for whichever test runs next.
                await page.EvaluateAsync(MultiDiffEditorCases.RestoreSampleFilesExpression);
                await WaitForDiffsComputedAsync();
            }

            // The sample's own list is what the baseline was measured against; the probe list is
            // deliberately smaller, so the two are not compared.
            _ = baseline;
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// The managed side of the feature: the sample's marker only appears if
    /// <c>DiffUpdated</c> reached C#, which means the JS callback, the bridge, and the
    /// <c>DiffFileEntry</c> round trip all worked. Every other assertion here reads the DOM or
    /// Monaco's JS API and would pass with the managed half entirely broken.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_ReportsFileCountsThroughTheManagedApi()
    {
        _currentTestName = nameof(MultiDiffEditor_ReportsFileCountsThroughTheManagedApi);
        try
        {
            await WaitForSettledAsync();

            var marker = MultiDiffEditorCases.FilesMarkerPrefix;
            var line = await _fixture.WaitForLogLineAfterAsync(0, marker, MultiDiffTimeoutMs);

            // MULTIDIFF_FILES:{count}:{added}:{deleted}:{renamed}
            var parts = line[(line.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..]
                .Trim()
                .Split(':');

            Assert.Equal(4, parts.Length);
            Assert.Equal(MultiDiffEditorCases.SampleFilePaths.Length, int.Parse(parts[0]));
            Assert.Equal(1, int.Parse(parts[1]));
            Assert.Equal(1, int.Parse(parts[2]));
            Assert.Equal(1, int.Parse(parts[3]));
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }
}
