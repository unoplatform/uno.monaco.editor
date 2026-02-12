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
    public async Task LanguageServices_CompletionProviderReturnsItems()
    {
        _currentTestName = nameof(LanguageServices_CompletionProviderReturnsItems);
        try
        {
            // The test app registers a CompletionItemProvider that returns a "foreach"
            // snippet by default. Invoke the provider programmatically and verify output.
            var completionLabels = await _fixture.Page.EvaluateAsync<string[]>("""
                async () => {
                    const editor = monaco.editor.getEditors()[0];
                    const model = editor.getModel();
                    const position = { lineNumber: 1, column: 1 };
                    const context = { triggerKind: 0 };
                    const token = new monaco.CancellationTokenSource().token;
                    const providers = monaco.languages.getCompletionItemProvider(model);
                    if (!providers || providers.length === 0) return [];
                    const result = await providers[0].provideCompletionItems(model, position, context, token);
                    if (!result || !result.suggestions) return [];
                    return result.suggestions.map(s => typeof s.label === 'string' ? s.label : (s.label?.label || ''));
                }
                """);

            Assert.NotNull(completionLabels);
            Assert.NotEmpty(completionLabels);
            Assert.Contains("foreach", completionLabels);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task LanguageServices_HoverProviderReturnsContent()
    {
        _currentTestName = nameof(LanguageServices_HoverProviderReturnsContent);
        try
        {
            // The test app's HoverProvider returns hover content for words containing "Hit".
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.getEditors()[0].setValue('HitTest word here')");

            var hoverContents = await _fixture.Page.EvaluateAsync<string[]>("""
                async () => {
                    const editor = monaco.editor.getEditors()[0];
                    const model = editor.getModel();
                    const position = { lineNumber: 1, column: 2 };
                    const token = new monaco.CancellationTokenSource().token;
                    const providers = monaco.languages.getHoverProvider(model);
                    if (!providers || providers.length === 0) return ['NO_PROVIDERS'];
                    const result = await providers[0].provideHover(model, position, token);
                    if (!result || !result.contents) return ['NO_RESULT'];
                    return result.contents.map(c => typeof c === 'string' ? c : (c.value || ''));
                }
                """);

            Assert.NotNull(hoverContents);
            Assert.NotEmpty(hoverContents);
            Assert.Contains(hoverContents, c => c.Contains("Hit"));
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task LanguageServices_CodeLensProviderReturnsLenses()
    {
        _currentTestName = nameof(LanguageServices_CodeLensProviderReturnsLenses);
        try
        {
            // The test app's CodeLensProvider returns a lens on line 2 titled "Second Line Command".
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.getEditors()[0].setValue('Line 1\\nLine 2\\nLine 3')");

            var codeLensTitles = await _fixture.Page.EvaluateAsync<string[]>("""
                async () => {
                    const editor = monaco.editor.getEditors()[0];
                    const model = editor.getModel();
                    const token = new monaco.CancellationTokenSource().token;
                    const providers = monaco.languages.getCodeLensProvider(model);
                    if (!providers || providers.length === 0) return ['NO_PROVIDERS'];
                    const result = await providers[0].provideCodeLenses(model, token);
                    if (!result || !result.lenses) return ['NO_LENSES'];
                    return result.lenses.map(l => l.command ? l.command.title : '(no command)');
                }
                """);

            Assert.NotNull(codeLensTitles);
            Assert.NotEmpty(codeLensTitles);
            Assert.Contains("Second Line Command", codeLensTitles);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task LanguageServices_ColorProviderDetectsColors()
    {
        _currentTestName = nameof(LanguageServices_ColorProviderDetectsColors);
        try
        {
            // The test app's ColorProvider detects 8-char hex colors (#AARRGGBB).
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.getEditors()[0].setValue('Color: #FF00FF00')");

            var colorCount = await _fixture.Page.EvaluateAsync<int>("""
                async () => {
                    const editor = monaco.editor.getEditors()[0];
                    const model = editor.getModel();
                    const token = new monaco.CancellationTokenSource().token;
                    const providers = monaco.languages.getColorProvider(model);
                    if (!providers || providers.length === 0) return -1;
                    const result = await providers[0].provideDocumentColors(model, token);
                    return result ? result.length : 0;
                }
                """);

            Assert.True(colorCount > 0,
                "Color provider should detect at least one hex color (#FF00FF00) in the text.");
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
    public async Task MultiInstance_EditorsHaveIndependentState()
    {
        _currentTestName = nameof(MultiInstance_EditorsHaveIndependentState);
        try
        {
            // Create a second Monaco editor instance in a new container.
            var editorCount = await _fixture.Page.EvaluateAsync<int>("""
                () => {
                    const container = document.createElement('div');
                    container.id = 'test-editor-2';
                    container.style.width = '400px';
                    container.style.height = '200px';
                    document.body.appendChild(container);
                    monaco.editor.create(container, { value: 'second editor', language: 'plaintext' });
                    return monaco.editor.getEditors().length;
                }
                """);

            Assert.True(editorCount >= 2, $"Expected at least 2 editors, got {editorCount}.");

            // Verify independent state: first editor has different text from second.
            var firstText = await _fixture.Page.EvaluateAsync<string>(
                "() => monaco.editor.getEditors()[0].getValue()");
            var secondText = await _fixture.Page.EvaluateAsync<string>("""
                () => {
                    const editors = monaco.editor.getEditors();
                    return editors[editors.length - 1].getValue();
                }
                """);

            Assert.Equal("second editor", secondText);
            Assert.NotEqual(firstText, secondText);

            // Cleanup: dispose the second editor and remove its container.
            await _fixture.Page.EvaluateAsync("""
                () => {
                    const editors = monaco.editor.getEditors();
                    const last = editors[editors.length - 1];
                    last.dispose();
                    const el = document.getElementById('test-editor-2');
                    if (el) el.remove();
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
/// <see cref="DesktopAppFixture"/> across all tests in the collection.
/// </summary>
[CollectionDefinition("DesktopCDP")]
public sealed class DesktopCdpCollection : ICollectionFixture<DesktopAppFixture>
{
}
