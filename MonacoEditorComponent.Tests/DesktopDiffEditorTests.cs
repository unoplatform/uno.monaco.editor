using Microsoft.Playwright;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Playwright CDP integration tests for <c>DiffCodeEditor</c> on the desktop
/// (Skia/WebView2) target.
///
/// <para>These drive <see cref="DesktopAppFixture.DiffPage"/>, a second WebView2 host that the
/// test app realizes when <c>MONACO_DIFF_TAB=1</c> is set. The diff sample cannot live in a
/// TabView tab for these tests: TabView virtualizes non-selected tab content, so the control
/// would never be constructed, and CDP reaches WebView contents rather than the XAML tree.</para>
///
/// <para>What this adds over the WASM run is the desktop-specific path: the
/// <c>createMonacoDiffEditor</c> bootstrap driven by the pushed initial-state payload, over
/// the JSON-RPC bridge, against the bundle copy in <c>DesktopContent/</c>.</para>
/// </summary>
[Trait("Category", "DesktopCDP")]
[Collection("DesktopCDP")]
public sealed class DesktopDiffEditorTests : IAsyncLifetime
{
    private const int DiffTimeoutMs = 30_000;

    private readonly DesktopAppFixture _fixture;
    private string _currentTestName = "unknown";
    private bool _testFailed;

    public DesktopDiffEditorTests(DesktopAppFixture fixture)
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
    /// The two documents arrive independently and both come from the pushed initial state, so
    /// this covers the desktop-only path where C# serializes originalText/originalLanguage/
    /// diffOptions into the bootstrap payload rather than pushing them afterwards.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task DiffEditor_LoadsBothDocumentsFromPushedInitialState()
    {
        _currentTestName = nameof(DiffEditor_LoadsBothDocumentsFromPushedInitialState);
        try
        {
            await _fixture.DiffPage.WaitForFunctionAsync(
                DiffEditorCases.IsDiffEditorPresentExpression,
                null, new PageWaitForFunctionOptions { Timeout = DiffTimeoutMs });

            Assert.True(
                await _fixture.DiffPage.EvaluateAsync<bool>(DiffEditorCases.ModelsAreDistinctExpression),
                "The original and modified sides must be backed by distinct models.");

            var original = await _fixture.DiffPage.EvaluateAsync<string>(DiffEditorCases.OriginalValueExpression);
            var modified = await _fixture.DiffPage.EvaluateAsync<string>(DiffEditorCases.ModifiedValueExpression);

            Assert.NotEqual(original, modified);
            Assert.StartsWith(DiffEditorCases.SharedFirstLine, original);
            Assert.StartsWith(DiffEditorCases.SharedFirstLine, modified);

            // OriginalLanguage is unset on the sample, so the original side must follow
            // CodeLanguage. Nothing in the base forwards language to the original model, so
            // this is the assertion that keeps DiffCodeEditor's own forwarding honest.
            Assert.Equal("csharp", await _fixture.DiffPage.EvaluateAsync<string>(DiffEditorCases.OriginalLanguageExpression));
            Assert.Equal("csharp", await _fixture.DiffPage.EvaluateAsync<string>(DiffEditorCases.ModifiedLanguageExpression));
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// Monaco computes the diff asynchronously, so the hunks are only observable after a wait.
    /// Editing the modified side must produce a fresh computation -- that recomputation is
    /// what drives <c>onDidUpdateDiff</c> and therefore the control's DiffUpdated event.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task DiffEditor_ComputesHunksAndRecomputesOnEdit()
    {
        _currentTestName = nameof(DiffEditor_ComputesHunksAndRecomputesOnEdit);
        try
        {
            await _fixture.DiffPage.WaitForFunctionAsync(
                DiffEditorCases.HasComputedDiffExpression,
                null, new PageWaitForFunctionOptions { Timeout = DiffTimeoutMs });

            var original = await _fixture.DiffPage.EvaluateAsync<string>(DiffEditorCases.OriginalValueExpression);
            var modified = await _fixture.DiffPage.EvaluateAsync<string>(DiffEditorCases.ModifiedValueExpression);

            // Make the sides identical: the hunk count must fall to zero, which proves the
            // diff is genuinely recomputed rather than captured once at construction.
            await _fixture.DiffPage.EvaluateAsync(DiffEditorCases.SetModifiedValueExpression, original);
            await _fixture.DiffPage.WaitForFunctionAsync(
                DiffEditorCases.NoRemainingHunksExpression,
                null, new PageWaitForFunctionOptions { Timeout = DiffTimeoutMs });

            // Restore, so the shared app is left as the other tests in this collection expect.
            await _fixture.DiffPage.EvaluateAsync(DiffEditorCases.SetModifiedValueExpression, modified);
            await _fixture.DiffPage.WaitForFunctionAsync(
                DiffEditorCases.HasComputedDiffExpression,
                null, new PageWaitForFunctionOptions { Timeout = DiffTimeoutMs });
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// The diff widget's own stylesheet rules are the one part of the CSS payload a plain
    /// editor never exercises, so this is also the cheapest check that the diff styles reach
    /// the page at all.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task DiffEditor_RendersDiffWidgetRoot()
    {
        _currentTestName = nameof(DiffEditor_RendersDiffWidgetRoot);
        try
        {
            await _fixture.DiffPage.WaitForFunctionAsync(
                DiffEditorCases.DiffEditorRootExpression,
                null, new PageWaitForFunctionOptions { Timeout = DiffTimeoutMs });
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }
}
