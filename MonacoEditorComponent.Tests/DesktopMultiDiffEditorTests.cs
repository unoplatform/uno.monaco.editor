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

    /// <summary>
    /// Brings the widget to a known state: present, showing the sample's four files, every diff
    /// computed.
    /// </summary>
    /// <remarks>
    /// The sample list is re-pushed rather than assumed, because the app is shared across the
    /// whole DesktopCDP collection and several tests here drive their own file lists. Restoring
    /// at the *start* of each test rather than in the previous one's finally means a test that
    /// fails part-way cannot cascade into the rest -- which it otherwise does, since the readiness
    /// gate is itself a wait on the widget's contents.
    /// </remarks>
    private async Task WaitForSettledAsync()
    {
        await _fixture.MultiDiffPage.WaitForFunctionAsync(
            MultiDiffEditorCases.IsPresentExpression,
            null, new PageWaitForFunctionOptions { Timeout = MultiDiffTimeoutMs });

        // Expand first: a collapsed file has its diff model detached, so the computed-diffs wait
        // below could never be satisfied after a test that left the list collapsed.
        await _fixture.MultiDiffPage.EvaluateAsync(MultiDiffEditorCases.SetAllCollapsedExpression, false);
        await _fixture.MultiDiffPage.EvaluateAsync(MultiDiffEditorCases.RestoreSampleFilesExpression);

        await _fixture.MultiDiffPage.WaitForFunctionAsync(
            MultiDiffEditorCases.AnyEntryRenderedExpression,
            null, new PageWaitForFunctionOptions { Timeout = MultiDiffTimeoutMs });
        await WaitForDiffsComputedAsync();
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
    /// The header label is the file name, with the containing directory as a separate span
    /// beside it. The rename carries its old path in the original-side label; every other file
    /// leaves that label empty.
    /// </summary>
    /// <remarks>
    /// Item templates are pooled and recycled across files, and the widget rebinds both labels on
    /// every reuse, so a label that returned early on an absent URI would leave the previous
    /// occupant's path showing. The forward pass catches that between non-renamed files; the
    /// trailing reveal of the first file catches it after the rename, whose template is the only
    /// one that ever carries a secondary label.
    /// </remarks>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_SplitsHeaderLabelIntoNameAndDirectory()
    {
        _currentTestName = nameof(MultiDiffEditor_SplitsHeaderLabelIntoNameAndDirectory);
        try
        {
            await WaitForSettledAsync();

            const string renamed = "docs/arithmetic.md";

            foreach (var path in MultiDiffEditorCases.SampleFilePaths)
            {
                var page = await RevealAsync(path);
                var parts = await page.EvaluateAsync<string[]>(
                    MultiDiffEditorCases.LabelPartsForPathExpression, path);
                var cut = path.LastIndexOf('/');

                Assert.Equal(path[(cut + 1)..], parts[0]);
                Assert.Equal(path[..cut], parts[1]);

                var secondary = await page.EvaluateAsync<string>(
                    MultiDiffEditorCases.SecondaryLabelForPathExpression, path);

                if (path == renamed)
                {
                    Assert.Contains("math.md", secondary);
                }
                else
                {
                    Assert.Equal(string.Empty, secondary);
                }
            }

            // Back to a file that is not a rename, now that the renamed one has been rendered:
            // its template is in the pool, and a label that returned early on an absent URI would
            // leave the old path showing beside a file that never had one.
            var first = MultiDiffEditorCases.SampleFilePaths[0];
            var reused = await RevealAsync(first);

            Assert.Equal(
                string.Empty,
                await reused.EvaluateAsync<string>(
                    MultiDiffEditorCases.SecondaryLabelForPathExpression, first));
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// Revealing a file *above* the one on screen, then pushing a file list, leaves the widget
    /// rendering.
    /// </summary>
    /// <remarks>
    /// The backward reveal is the whole point. Jumping the scroll position in one step used to
    /// churn the widget's derived-observable graph inside a single transaction and leak three
    /// unmatched <c>beginUpdate</c> calls onto its "render all" autorun; an autorun with a
    /// non-zero update count never runs again. Nothing looked wrong until the next push released
    /// every pooled template -- with no render pass left to re-acquire one, the list went blank
    /// permanently, and neither a resize nor another scroll brought it back. In user terms:
    /// scroll up, update <c>Files</c>, lose the list.
    /// <para>
    /// <c>revealMultiDiffFile</c> now scrolls smoothly, spreading the change across animation
    /// frames the way a mouse wheel does -- wheel scrolling never reproduced this. The assertion
    /// that matters is the one *after* the push: the reveal itself succeeds either way, and so
    /// does everything up to the push.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_KeepsRenderingAfterAPushThatFollowsABackwardReveal()
    {
        _currentTestName = nameof(MultiDiffEditor_KeepsRenderingAfterAPushThatFollowsABackwardReveal);
        try
        {
            await WaitForSettledAsync();

            // Forward to the end, so the pool has recycled and the viewport is off the top.
            foreach (var path in MultiDiffEditorCases.SampleFilePaths)
            {
                await RevealAsync(path);
            }

            // ...then back to the first file, which is what used to wedge the render autorun.
            await RevealAsync(MultiDiffEditorCases.SampleFilePaths[0]);

            // The same gate every other test opens with. Expanding before the push is what makes
            // the damage visible: the push releases every pooled template, and a wedged autorun
            // never re-acquires one.
            await WaitForSettledAsync();
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

                // Monaco's stylesheet asks for font-weight 600 through a rule carrying the whole
                // widget selector chain; the component overrides it back to normal. Nothing else
                // observes that override, and it fails by simply losing the cascade.
                var weight = await page.EvaluateAsync<string>(
                    MultiDiffEditorCases.BadgeFontWeightForPathExpression, path);

                Assert.Equal("400", weight);
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

            // The per-file editors are ordinary diff editors behind a different root, so the
            // change-marker override has to reach them too.
            Assert.Equal(
                "0.4",
                await _fixture.MultiDiffPage.EvaluateAsync<string>(MultiDiffEditorCases.ChangeSignOpacityExpression));
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
    /// A file's scroll position and collapsed state must survive a text change to a *different*
    /// file.
    /// </summary>
    /// <remarks>
    /// This is the only assertion that the push is genuinely incremental. Every change -- one
    /// entry's text, an add, a remove -- re-sends the whole list, which is only acceptable
    /// because <c>updateMultiDiffFiles</c> reconciles by path and hands Monaco the *same*
    /// IDocumentDiffItem object for an unchanged file, so <c>mapObservableArrayCached</c> keeps
    /// its view model. A regression to rebuilding everything renders identically and passes every
    /// other test here, while silently resetting scroll and collapse on each keystroke.
    /// </remarks>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_PreservesScrollAndCollapseAcrossAnUnrelatedEdit()
    {
        _currentTestName = nameof(MultiDiffEditor_PreservesScrollAndCollapseAcrossAnUnrelatedEdit);
        try
        {
            await WaitForSettledAsync();
            var page = _fixture.MultiDiffPage;

            try
            {
                // Tall files, so there is real scrolling to preserve.
                await page.EvaluateAsync(MultiDiffEditorCases.PushTallProbesExpression());
                await WaitForDiffsComputedAsync();

                const string target = "probe/three.cs";
                await page.EvaluateAsync(MultiDiffEditorCases.RevealPathExpression, target);
                await page.WaitForFunctionAsync(
                    MultiDiffEditorCases.IsPathRenderedExpression, target,
                    new PageWaitForFunctionOptions { Timeout = MultiDiffTimeoutMs });

                await page.EvaluateAsync(MultiDiffEditorCases.SetCollapsedExpression, new object[] { target, true });
                await page.WaitForFunctionAsync(
                    MultiDiffEditorCases.IsPathCollapsedExpression, target,
                    new PageWaitForFunctionOptions { Timeout = MultiDiffTimeoutMs });

                // Change the FIRST file's text. Nothing about the third file changed, so neither
                // its position in the scroll nor its collapsed state may move.
                await page.EvaluateAsync(MultiDiffEditorCases.PushTallProbesExpression("// edited"));
                await Task.Delay(1500);

                Assert.True(
                    await page.EvaluateAsync<bool>(MultiDiffEditorCases.IsPathRenderedExpression, target),
                    "Editing another file scrolled the view away from the revealed file, so the push rebuilt the list instead of reconciling it.");
                Assert.True(
                    await page.EvaluateAsync<bool?>(MultiDiffEditorCases.IsPathCollapsedExpression, target),
                    "Editing another file expanded a collapsed one, so its view model was recreated.");
            }
            finally
            {
                // WaitForSettledAsync restores the sample list for whichever test runs next, so
                // there is nothing to undo here beyond letting this one's pushes drain.
                await Task.Delay(200);
            }
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// An absent side and an empty side must render differently: <c>null</c> earns the <c>A</c>
    /// badge, <c>""</c> is an ordinary diff against an empty file.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_DistinguishesAnAbsentSideFromAnEmptyOne()
    {
        _currentTestName = nameof(MultiDiffEditor_DistinguishesAnAbsentSideFromAnEmptyOne);
        try
        {
            await WaitForSettledAsync();
            var page = _fixture.MultiDiffPage;

            try
            {
                await page.EvaluateAsync(MultiDiffEditorCases.PushNullVersusEmptyExpression);
                await WaitForDiffsComputedAsync();

                foreach (var (path, badge) in new[] { ("probe/added.cs", "A"), ("probe/emptied.cs", string.Empty) })
                {
                    // Revealed first: the panel is short enough that the second file has no DOM
                    // until it is scrolled to.
                    await page.EvaluateAsync(MultiDiffEditorCases.RevealPathExpression, path);
                    await page.WaitForFunctionAsync(
                        MultiDiffEditorCases.IsPathRenderedExpression, path,
                        new PageWaitForFunctionOptions { Timeout = MultiDiffTimeoutMs });

                    Assert.Equal(badge, await page.EvaluateAsync<string>(
                        MultiDiffEditorCases.BadgeForPathExpression, path));
                }
            }
            finally
            {
                // WaitForSettledAsync restores the sample list for whichever test runs next, so
                // there is nothing to undo here beyond letting this one's pushes drain.
                await Task.Delay(200);
            }
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

    /// <summary>
    /// A file whose shape changes -- here an added file gaining an original side -- forces the
    /// entry to be rebuilt rather than updated in place. The rebuilt entry adopts the models
    /// whose URIs did not change (the modified URI is derived from the path alone), while the
    /// outgoing entry is disposed two frames later. Nothing else in this suite drives a shape
    /// change, so without this the rebuilt entry could be left holding a disposed model.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task MultiDiffEditor_KeepsModelsAliveWhenAFileChangesShape()
    {
        _currentTestName = nameof(MultiDiffEditor_KeepsModelsAliveWhenAFileChangesShape);
        try
        {
            await WaitForSettledAsync();

            var page = _fixture.MultiDiffPage;
            const string path = "probe/shape.cs";

            try
            {
                await page.EvaluateAsync(MultiDiffEditorCases.PushShapeChangeAddedExpression);
                await WaitForDiffsComputedAsync();
                Assert.Equal(1, await page.EvaluateAsync<int>(
                    MultiDiffEditorCases.LiveModelsForPathExpression, path));

                // Same path, now with an original side: a rebuild, not an in-place update.
                await page.EvaluateAsync(MultiDiffEditorCases.PushShapeChangeModifiedExpression);
                await WaitForDiffsComputedAsync();

                // Past the double-requestAnimationFrame the deferred disposal waits on, plus
                // margin -- the point is to observe the state the disposal leaves behind.
                await Task.Delay(500);

                Assert.Equal(2, await page.EvaluateAsync<int>(
                    MultiDiffEditorCases.LiveModelsForPathExpression, path));

                // A disposed model still renders its last frame, so assert the widget can
                // actually read through to it rather than trusting the count alone.
                await page.EvaluateAsync(MultiDiffEditorCases.RevealPathExpression, path);
                await page.WaitForFunctionAsync(
                    MultiDiffEditorCases.IsPathRenderedExpression, path,
                    new PageWaitForFunctionOptions { Timeout = MultiDiffTimeoutMs });
            }
            finally
            {
                await page.EvaluateAsync(MultiDiffEditorCases.ClearFilesExpression);
                await Task.Delay(200);
            }
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }
}
