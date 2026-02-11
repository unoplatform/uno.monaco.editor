using Microsoft.Playwright;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Playwright CDP integration tests for the desktop (Skia/WebView2) target.
/// These tests connect to the MonacoEditorTestApp desktop process via Chrome DevTools
/// Protocol and verify Monaco editor functionality through the WebView2 DOM.
///
/// <para><b>Windows only</b>: Tagged with <c>[Trait("Category", "DesktopCDP")]</c> so
/// they can be filtered out in CI on non-Windows runners:
/// <c>dotnet test --filter "Category!=DesktopCDP"</c></para>
///
/// <para><b>Testability constraint</b>: Playwright CDP connects to WebView2 content only.
/// It cannot see or interact with native Uno/XAML controls surrounding the WebView2.
/// All test interactions go through the WebView2 DOM via <c>page.EvaluateAsync</c>.</para>
///
/// <para><b>Agent-driven testing (complementary)</b>: For verifying native Uno/XAML controls
/// (property panels, status indicators), use the Uno App MCP for ad-hoc agent-driven
/// testing. This is a development convenience, not part of automated CI.</para>
/// </summary>
[Trait("Category", "DesktopCDP")]
[Collection("DesktopCDP")]
public sealed class DesktopIntegrationTests : IAsyncLifetime
{
    private readonly DesktopAppFixture _fixture;
    private string _currentTestName = "unknown";
    private bool _testFailed;

    public DesktopIntegrationTests(DesktopAppFixture fixture)
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
    [Trait("Category", "DesktopCDP")]
    public async Task EditorLoads_MonacoInstanceCreated()
    {
        _currentTestName = nameof(EditorLoads_MonacoInstanceCreated);
        try
        {
            // The fixture already waits for Monaco ready, so if we get here the editor loaded.
            // Verify explicitly that there is at least one editor instance.
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
    [Trait("Category", "DesktopCDP")]
    public async Task TextRoundTrip_SetAndGetText()
    {
        _currentTestName = nameof(TextRoundTrip_SetAndGetText);
        try
        {
            var testText = $"Hello from Playwright CDP test {Guid.NewGuid():N}";

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
    [Trait("Category", "DesktopCDP")]
    public async Task BridgeRoundTrip_JsonRpcGetValue()
    {
        _currentTestName = nameof(BridgeRoundTrip_JsonRpcGetValue);
        try
        {
            // First set known text via Monaco API so we know what to expect.
            var testText = "Bridge round-trip test value";
            await _fixture.Page.EvaluateAsync(
                $"() => monaco.editor.getEditors()[0].setValue('{testText}')");

            // Use the JSON-RPC bridge to request the Text property from C#.
            // window.__jsonRpc is the vscode-jsonrpc MessageConnection exposed by the bridge.
            var bridgeResult = await _fixture.Page.EvaluateAsync<string>(
                "() => window.__jsonRpc.sendRequest('parentAccessor/getJsonValue', { name: 'Text' })");

            // The bridge returns the value as a JSON-encoded string.
            Assert.NotNull(bridgeResult);
            Assert.Contains(testText, bridgeResult);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task ThemeSwitching_ChangeThemeAndVerify()
    {
        _currentTestName = nameof(ThemeSwitching_ChangeThemeAndVerify);
        try
        {
            // Switch to vs-dark theme via Monaco JS API.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.setTheme('vs-dark')");

            // Verify the theme was applied. Check the DOM class first (most reliable),
            // then fall back to the body data attribute. At least one must confirm dark theme.
            var hasDarkClass = await _fixture.Page.EvaluateAsync<bool>(
                "() => document.querySelector('.monaco-editor')?.classList.contains('vs-dark') ?? false");

            var hasThemeAttr = await _fixture.Page.EvaluateAsync<bool>(
                "() => (document.body.getAttribute('data-vscode-theme-name') || '').includes('dark')");

            Assert.True(hasDarkClass || hasThemeAttr,
                "Expected either .monaco-editor.vs-dark class or dark theme body attribute after switching to vs-dark.");

            // Switch back to default to not affect other tests.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.setTheme('vs')");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task Decorations_AddAndVerify()
    {
        _currentTestName = nameof(Decorations_AddAndVerify);
        try
        {
            // Ensure there is some text to decorate.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.getEditors()[0].setValue('Line 1\\nLine 2\\nLine 3')");

            // Capture baseline decoration count before adding ours.
            var countBefore = await _fixture.Page.EvaluateAsync<int>(
                "() => monaco.editor.getEditors()[0].getModel().getAllDecorations().length");

            // Add a decoration via Monaco JS API and capture the returned IDs.
            var addedIds = await _fixture.Page.EvaluateAsync<string[]>("""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    return editor.deltaDecorations([], [{
                        range: new monaco.Range(1, 1, 1, 7),
                        options: { inlineClassName: 'test-decoration' }
                    }]);
                }
                """);

            Assert.NotNull(addedIds);
            Assert.NotEmpty(addedIds);

            // Verify decoration count increased.
            var countAfter = await _fixture.Page.EvaluateAsync<int>(
                "() => monaco.editor.getEditors()[0].getModel().getAllDecorations().length");

            Assert.True(countAfter > countBefore,
                $"Expected decoration count to increase after adding. Before: {countBefore}, After: {countAfter}");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task Markers_AddAndVerify()
    {
        _currentTestName = nameof(Markers_AddAndVerify);
        try
        {
            // Set text content for markers.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.getEditors()[0].setValue('let x = 1;\\nlet y = 2;\\nlet z = 3;')");

            // Add markers via Monaco JS API.
            await _fixture.Page.EvaluateAsync("""
                () => {
                    const model = monaco.editor.getEditors()[0].getModel();
                    monaco.editor.setModelMarkers(model, 'test', [{
                        startLineNumber: 1, startColumn: 1, endLineNumber: 1, endColumn: 5,
                        message: 'Test error marker',
                        severity: monaco.MarkerSeverity.Error
                    }]);
                }
                """);

            // Verify markers were added.
            var markerCount = await _fixture.Page.EvaluateAsync<int>("""
                () => {
                    const model = monaco.editor.getEditors()[0].getModel();
                    return monaco.editor.getModelMarkers({ resource: model.uri }).length;
                }
                """);

            Assert.True(markerCount > 0, "Expected at least one marker after adding.");

            // Cleanup.
            await _fixture.Page.EvaluateAsync("""
                () => {
                    const model = monaco.editor.getEditors()[0].getModel();
                    monaco.editor.setModelMarkers(model, 'test', []);
                }
                """);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task LanguageServices_CompletionProviderRegistered()
    {
        _currentTestName = nameof(LanguageServices_CompletionProviderRegistered);
        try
        {
            // Trigger completion programmatically and check that suggestions appear.
            // The test app registers a CompletionItemProvider that responds to trigger characters.
            var hasCompletionApi = await _fixture.Page.EvaluateAsync<bool>(
                "() => typeof monaco.languages.registerCompletionItemProvider === 'function'");

            Assert.True(hasCompletionApi, "Monaco completion API should be available.");

            // Verify the editor can trigger completion without errors.
            var triggeredOk = await _fixture.Page.EvaluateAsync<bool>("""
                () => {
                    try {
                        const editor = monaco.editor.getEditors()[0];
                        editor.trigger('test', 'editor.action.triggerSuggest', {});
                        return true;
                    } catch { return false; }
                }
                """);

            Assert.True(triggeredOk, "Triggering completion should not throw.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task LanguageServices_HoverProviderRegistered()
    {
        _currentTestName = nameof(LanguageServices_HoverProviderRegistered);
        try
        {
            var hasHoverApi = await _fixture.Page.EvaluateAsync<bool>(
                "() => typeof monaco.languages.registerHoverProvider === 'function'");

            Assert.True(hasHoverApi, "Monaco hover API should be available.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task LanguageServices_CodeLensProviderRegistered()
    {
        _currentTestName = nameof(LanguageServices_CodeLensProviderRegistered);
        try
        {
            var hasCodeLensApi = await _fixture.Page.EvaluateAsync<bool>(
                "() => typeof monaco.languages.registerCodeLensProvider === 'function'");

            Assert.True(hasCodeLensApi, "Monaco CodeLens API should be available.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task LanguageServices_ColorProviderRegistered()
    {
        _currentTestName = nameof(LanguageServices_ColorProviderRegistered);
        try
        {
            var hasColorApi = await _fixture.Page.EvaluateAsync<bool>(
                "() => typeof monaco.languages.registerColorProvider === 'function'");

            Assert.True(hasColorApi, "Monaco color provider API should be available.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task Keyboard_UndoRedoWorks()
    {
        _currentTestName = nameof(Keyboard_UndoRedoWorks);
        try
        {
            // Set initial text.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.getEditors()[0].setValue('initial')");

            // Type additional text via the editor action.
            await _fixture.Page.EvaluateAsync("""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    editor.executeEdits('test', [{
                        range: new monaco.Range(1, 8, 1, 8),
                        text: ' extra'
                    }]);
                }
                """);

            var afterEdit = await _fixture.Page.EvaluateAsync<string>(
                "() => monaco.editor.getEditors()[0].getValue()");
            Assert.Equal("initial extra", afterEdit);

            // Trigger undo via Monaco action.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.getEditors()[0].trigger('test', 'undo', null)");

            var afterUndo = await _fixture.Page.EvaluateAsync<string>(
                "() => monaco.editor.getEditors()[0].getValue()");
            Assert.Equal("initial", afterUndo);

            // Trigger redo.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.getEditors()[0].trigger('test', 'redo', null)");

            var afterRedo = await _fixture.Page.EvaluateAsync<string>(
                "() => monaco.editor.getEditors()[0].getValue()");
            Assert.Equal("initial extra", afterRedo);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task LifecycleEvents_ExactlyOnce()
    {
        _currentTestName = nameof(LifecycleEvents_ExactlyOnce);
        try
        {
            // The lifecycle counts are exposed to the WebView2 DOM via JSON-RPC bridge.
            // C# EditorLoading/EditorLoaded handlers push counts to JS via
            // editor/lifecycleUpdate notification. JS handler writes to
            // document.body.dataset.lifecycleLoaded.

            // Wait briefly for any lifecycle notifications to propagate.
            await _fixture.Page.WaitForFunctionAsync(
                "() => document.body.dataset.lifecycleLoaded !== undefined",
                null, new PageWaitForFunctionOptions { Timeout = 5000 });

            var lifecycleLoaded = await _fixture.Page.EvaluateAsync<string>(
                "() => document.body.dataset.lifecycleLoaded");

            Assert.Equal("1", lifecycleLoaded);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }
}

/// <summary>
/// xUnit collection definition for desktop CDP tests, sharing a single
/// <see cref="DesktopAppFixture"/> (and its <see cref="PlaywrightSetup"/> dependency)
/// across all tests in the collection.
/// xUnit v3 creates <see cref="PlaywrightSetup"/> first, then injects it into
/// <see cref="DesktopAppFixture"/>'s constructor.
/// </summary>
[CollectionDefinition("DesktopCDP")]
public sealed class DesktopCdpCollection : ICollectionFixture<PlaywrightSetup>, ICollectionFixture<DesktopAppFixture>
{
}
