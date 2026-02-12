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
                "() => monaco.editor.getEditors().length");

            Assert.True(editorCount > 0, "Expected at least one Monaco editor instance.");
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

            // Set text via Monaco JS API.
            await _fixture.Page.EvaluateAsync(
                $"() => monaco.editor.getEditors()[0].setValue('{testText}')");

            // Read back via Monaco JS API.
            var readBack = await _fixture.Page.EvaluateAsync<string>(
                "() => monaco.editor.getEditors()[0].getValue()");

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
            // Verify exactly one editor instance exists (lifecycle fired once, not duplicated).
            var editorCount = await _fixture.Page.EvaluateAsync<int>(
                "() => monaco.editor.getEditors().length");

            Assert.Equal(1, editorCount);

            // Verify the editor has a model (fully loaded, not partial init).
            var hasModel = await _fixture.Page.EvaluateAsync<bool>(
                "() => monaco.editor.getEditors()[0].getModel() !== null");

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
}

/// <summary>
/// xUnit collection definition for WASM Playwright tests, sharing a single
/// <see cref="WasmAppFixture"/> across all tests in the collection.
/// </summary>
[CollectionDefinition("WasmPlaywright")]
public sealed class WasmPlaywrightCollection : ICollectionFixture<WasmAppFixture>
{
}
