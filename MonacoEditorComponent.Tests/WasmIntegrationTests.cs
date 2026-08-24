using Microsoft.Playwright;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Playwright browser integration tests for the WASM target.
/// These tests serve the pre-built WASM app and verify Monaco editor functionality
/// through standard Playwright browser automation.
///
/// <para><b>Any OS</b>: These tests run on any OS with Playwright Chromium installed.
/// Included in CI on <c>ubuntu-latest</c> via <c>dotnet test --filter "Category!=DesktopCDP"</c>.</para>
///
/// <para><b>Lighter coverage</b>: WASM is the established path. These tests ensure
/// the desktop work does not regress WASM functionality. Desktop tests have deeper
/// coverage of bridge, lifecycle, and decorations.</para>
/// </summary>
[Trait("Category", "WasmPlaywright")]
[Collection("WasmPlaywright")]
public sealed class WasmIntegrationTests : IAsyncLifetime
{
    private const int DiffTimeoutMs = 30_000;

    private readonly WasmAppFixture _fixture;
    private string _currentTestName = "unknown";
    private bool _testFailed;

    public WasmIntegrationTests(WasmAppFixture fixture)
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

    [Fact]
    [Trait("Category", "WasmPlaywright")]
    public async Task EditorLoads_MonacoInstanceCreated()
    {
        _currentTestName = nameof(EditorLoads_MonacoInstanceCreated);
        try
        {
            // The fixture already waits for Monaco ready.
            var editorCount = await _fixture.Page.EvaluateAsync<int>(
                DiffEditorCases.StandaloneEditorCountExpression);

            Assert.True(editorCount > 0, "Expected at least one standalone Monaco editor instance.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "WasmPlaywright")]
    public async Task BasicTextEditing_SetAndGetText()
    {
        _currentTestName = nameof(BasicTextEditing_SetAndGetText);
        try
        {
            var testText = $"WASM Playwright test {Guid.NewGuid():N}";

            // Target the plain editor explicitly: getEditors() also lists the diff widget's two
            // sub-editors, so indexing into it would depend on construction order.
            await _fixture.Page.EvaluateAsync(
                $"() => {DiffEditorCases.StandaloneEditorsExpressionBody}[0].setValue('{testText}')");

            // Read back via Monaco JS API.
            var readBack = await _fixture.Page.EvaluateAsync<string>(
                $"() => {DiffEditorCases.StandaloneEditorsExpressionBody}[0].getValue()");

            Assert.Equal(testText, readBack);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "WasmPlaywright")]
    public async Task LifecycleEvents_EditorLoadedExactlyOnce()
    {
        _currentTestName = nameof(LifecycleEvents_EditorLoadedExactlyOnce);
        try
        {
            // Verify exactly one plain editor instance exists (lifecycle fired once, not
            // duplicated). Counted excluding diff sub-editors -- see
            // DiffEditorCases.StandaloneEditorsExpressionBody.
            var editorCount = await _fixture.Page.EvaluateAsync<int>(
                DiffEditorCases.StandaloneEditorCountExpression);

            Assert.Equal(1, editorCount);

            // Verify the editor has a model (fully loaded, not partial init).
            var hasModel = await _fixture.Page.EvaluateAsync<bool>(
                DiffEditorCases.StandaloneEditorHasModelExpression);

            Assert.True(hasModel, "Editor should have a model after lifecycle completes.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "WasmPlaywright")]
    public async Task ThemeSwitching_ChangeThemeAndVerify()
    {
        _currentTestName = nameof(ThemeSwitching_ChangeThemeAndVerify);
        try
        {
            // Switch to vs-dark theme.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.setTheme('vs-dark')");

            // Verify the theme was applied via DOM class or body attribute.
            var hasDarkClass = await _fixture.Page.EvaluateAsync<bool>(
                "() => document.querySelector('.monaco-editor')?.classList.contains('vs-dark') ?? false");

            var hasThemeAttr = await _fixture.Page.EvaluateAsync<bool>(
                "() => (document.body.getAttribute('data-vscode-theme-name') || '').includes('dark')");

            Assert.True(hasDarkClass || hasThemeAttr,
                "Expected either .monaco-editor.vs-dark class or dark theme body attribute after switching to vs-dark.");

            // Switch back to default.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.setTheme('vs')");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// Monaco ships no <c>diff</c> grammar, so the component bundles and registers one.
    /// Running the real tokenizer is the only way to confirm the grammar compiles and that
    /// its rule order resolves the ambiguous markers correctly -- see
    /// <see cref="DiffLanguageTokenizationCases"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "WasmPlaywright")]
    public async Task DiffLanguage_RegisteredAndTokenizesAllDialects()
    {
        _currentTestName = nameof(DiffLanguage_RegisteredAndTokenizesAllDialects);
        try
        {
            var isRegistered = await _fixture.Page.EvaluateAsync<bool>(
                DiffLanguageTokenizationCases.IsRegisteredExpression);

            Assert.True(isRegistered, "Expected the bundled 'diff' language to be registered at bundle load.");

            var tokenTypes = await _fixture.Page.EvaluateAsync<string>(
                DiffLanguageTokenizationCases.TokenizeExpression,
                DiffLanguageTokenizationCases.Sample);

            Assert.Equal(DiffLanguageTokenizationCases.ExpectedTokens, tokenTypes);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// Covers <c>DiffCodeEditor</c> end to end on WASM: both documents load independently,
    /// the original side follows CodeLanguage, the widget renders, and the diff genuinely
    /// recomputes when the modified document changes.
    /// </summary>
    /// <remarks>
    /// On WASM there is no editor web worker in the payload, so Monaco computes the diff on
    /// the main thread through its built-in fallback. This is therefore also the check that
    /// that fallback path actually produces hunks. See <see cref="DiffEditorCases"/>.
    /// </remarks>
    [Fact]
    [Trait("Category", "WasmPlaywright")]
    public async Task DiffEditor_LoadsBothSidesAndRecomputesOnEdit()
    {
        _currentTestName = nameof(DiffEditor_LoadsBothSidesAndRecomputesOnEdit);
        try
        {
            await _fixture.Page.WaitForFunctionAsync(
                DiffEditorCases.HasComputedDiffExpression,
                null, new PageWaitForFunctionOptions { Timeout = DiffTimeoutMs });

            Assert.True(
                await _fixture.Page.EvaluateAsync<bool>(DiffEditorCases.ModelsAreDistinctExpression),
                "The original and modified sides must be backed by distinct models.");

            Assert.True(
                await _fixture.Page.EvaluateAsync<bool>(DiffEditorCases.DiffEditorRootExpression),
                "Expected Monaco's .monaco-diff-editor root element on the page.");

            var original = await _fixture.Page.EvaluateAsync<string>(DiffEditorCases.OriginalValueExpression);
            var modified = await _fixture.Page.EvaluateAsync<string>(DiffEditorCases.ModifiedValueExpression);

            Assert.NotEqual(original, modified);
            Assert.StartsWith(DiffEditorCases.SharedFirstLine, original);
            Assert.StartsWith(DiffEditorCases.SharedFirstLine, modified);

            // OriginalLanguage is unset on the sample, so the original side must follow
            // CodeLanguage. Nothing on the base forwards language to the original model, so
            // this is what keeps DiffCodeEditor's own forwarding honest.
            Assert.Equal("csharp", await _fixture.Page.EvaluateAsync<string>(DiffEditorCases.OriginalLanguageExpression));
            Assert.Equal("csharp", await _fixture.Page.EvaluateAsync<string>(DiffEditorCases.ModifiedLanguageExpression));

            // Make the sides identical: the hunk count must fall to zero, proving the diff is
            // recomputed rather than captured once at construction.
            await _fixture.Page.EvaluateAsync(DiffEditorCases.SetModifiedValueExpression, original);
            await _fixture.Page.WaitForFunctionAsync(
                DiffEditorCases.NoRemainingHunksExpression,
                null, new PageWaitForFunctionOptions { Timeout = DiffTimeoutMs });

            // Restore, so the shared page is left as other tests in this collection expect.
            await _fixture.Page.EvaluateAsync(DiffEditorCases.SetModifiedValueExpression, modified);
            await _fixture.Page.WaitForFunctionAsync(
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
    /// The two documents lock independently -- <c>OriginalEditable</c> governs the original side
    /// and the inherited <c>ReadOnly</c> the modified one -- and the sample leaves both at their
    /// defaults, so the original must come up read-only and the modified writable.
    /// </summary>
    /// <remarks>
    /// Nothing here drives <c>OriginalEditable</c> itself: the sample's toggle is a XAML control,
    /// which CDP cannot reach on desktop, so the two sides' default arrangement is what the
    /// integration suite can pin. The pass-through in both directions is covered by the sample
    /// manually instead.
    /// </remarks>
    [Fact]
    [Trait("Category", "WasmPlaywright")]
    public async Task DiffEditor_LocksTheOriginalSideOnly()
    {
        _currentTestName = nameof(DiffEditor_LocksTheOriginalSideOnly);
        try
        {
            // The diff options arrive after construction on WASM, so wait rather than read
            // once: a diff editor that is merely present may not have been configured yet.
            await _fixture.Page.WaitForFunctionAsync(
                DiffEditorCases.OriginalEditorLockedExpression,
                null, new PageWaitForFunctionOptions { Timeout = DiffTimeoutMs });

            Assert.False(
                await _fixture.Page.EvaluateAsync<bool>(DiffEditorCases.ModifiedEditorReadOnlyExpression),
                "The original side's lock must not leak onto the modified side, which ReadOnly governs.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// Monaco's bundled stylesheet has to be delivered to the page, not merely embedded in the
    /// assembly. It carries the layout rules and the codicon <c>@font-face</c>; without it the
    /// editor still runs, because Monaco sets much of its geometry inline, so no other test in
    /// this suite notices. What breaks is visual: icons render as tofu, action-bar lists keep
    /// their default bullets, and the line-number margin loses its positioning.
    /// </summary>
    [Fact]
    [Trait("Category", "WasmPlaywright")]
    public async Task MonacoStylesheet_IsDeliveredToThePage()
    {
        _currentTestName = nameof(MonacoStylesheet_IsDeliveredToThePage);
        try
        {
            Assert.True(
                await _fixture.Page.EvaluateAsync<bool>(DiffEditorCases.MonacoStylesheetAppliedExpression),
                "Monaco's stylesheet did not reach the document: no exact '.monaco-editor' rule, "
                + "no 'position: relative' on the editor, or no codicon font face. On WASM this "
                + "means the embedded resource is not under a WasmCSS logical name.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }
}

/// <summary>
/// xUnit collection definition for WASM Playwright tests, sharing a single
/// <see cref="WasmAppFixture"/> across all tests in the collection.
/// </summary>
[CollectionDefinition("WasmPlaywright")]
public sealed class WasmPlaywrightCollection : ICollectionFixture<WasmAppFixture>
{
}
