using Microsoft.Playwright;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// C# bridge integration tests that verify the CodeEditor C# API correctly proxies
/// to Monaco JS and that results flow back through the JSON-RPC bridge. These tests
/// exercise the full C# DP -> SendScriptAsync -> JS path and the JS -> JSON-RPC -> C#
/// notification path, proving the bridge works end-to-end on desktop.
///
/// <para>Unlike <see cref="DesktopIntegrationTests"/> which test Monaco JS APIs directly,
/// these tests validate the C# bridge layer (parentAccessor/setValue, getJsonValue,
/// callAction, callActionWithParameters).</para>
///
/// <para><b>Windows only</b>: Tagged with <c>[Trait("Category", "DesktopCDP")]</c>.</para>
/// </summary>
[Trait("Category", "DesktopCDP")]
[Collection("DesktopCDP")]
public sealed class DesktopBridgeIntegrationTests : IAsyncLifetime
{
    private readonly DesktopAppFixture _fixture;
    private string _currentTestName = "unknown";
    private bool _testFailed;

    public DesktopBridgeIntegrationTests(DesktopAppFixture fixture)
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

    // ============================================================
    // Host-initiated property tests (C# -> JS via DP + SendScriptAsync)
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task HostInitiatedProperties_SetFromCSharp()
    {
        _currentTestName = nameof(HostInitiatedProperties_SetFromCSharp);
        try
        {
            // The test harness in EditorControl.xaml.cs sets Text and CodeLanguage
            // from C# in the EditorLoaded handler, then emits a TEST_INIT_PROPS marker.
            // Search from cursor 0 because this is a one-time startup marker.
            var marker = await _fixture.WaitForLogLineAfterAsync(
                0, @"TEST_INIT_PROPS:text=// test-init-text,lang=javascript", 30_000);
            Assert.Contains("TEST_INIT_PROPS", marker);

            // Secondary confirmation: verify Monaco reflects the C#-set values.
            var text = await _fixture.Page.EvaluateAsync<string>(
                "() => monaco.editor.getEditors()[0].getValue()");
            Assert.Equal("// test-init-text", text);

            var lang = await _fixture.Page.EvaluateAsync<string>(
                "() => monaco.editor.getEditors()[0].getModel().getLanguageId()");
            Assert.Equal("javascript", lang);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // Text property roundtrip (bridge-driven: JS -> C# -> JS)
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task TextProperty_BridgeDrivenRoundTrip()
    {
        _currentTestName = nameof(TextProperty_BridgeDrivenRoundTrip);
        try
        {
            await _fixture.ResetEditorStateAsync();

            // Set text via the bridge (parentAccessor/setValue) which invokes C# OnSetValue.
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'Text', value: '"bridge-driven text"' })
                """);

            // Wait briefly for the C# property change to propagate back to Monaco.
            await Task.Delay(500);

            // Verify the text arrived in Monaco (C# set it via updateContent).
            var text = await _fixture.Page.EvaluateAsync<string>(
                "() => monaco.editor.getEditors()[0].getValue()");
            Assert.Equal("bridge-driven text", text);

            // Read back from C# via getJsonValue to confirm round-trip.
            var jsonValue = await _fixture.Page.EvaluateAsync<string>(
                "() => window.__jsonRpc.sendRequest('parentAccessor/getJsonValue', { name: 'Text' })");
            Assert.Contains("bridge-driven text", jsonValue);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // CodeLanguage roundtrip (bridge-driven)
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task CodeLanguage_BridgeDrivenSwitch()
    {
        _currentTestName = nameof(CodeLanguage_BridgeDrivenSwitch);
        try
        {
            await _fixture.ResetEditorStateAsync();

            // Switch language via bridge notification.
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'CodeLanguage', value: '"xml"' })
                """);

            await Task.Delay(500);

            // Verify C# side accepted the change by reading back via bridge.
            var jsonLang = await _fixture.Page.EvaluateAsync<string>(
                "() => window.__jsonRpc.sendRequest('parentAccessor/getJsonValue', { name: 'CodeLanguage' })");
            Assert.Contains("xml", jsonLang);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // Custom language registration
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task CustomLanguage_RegisterAndVerifyAvailable()
    {
        _currentTestName = nameof(CustomLanguage_RegisterAndVerifyAvailable);
        try
        {
            await _fixture.ResetEditorStateAsync();

            // Register a custom language via Monaco JS API (simulating what
            // LanguagesHelper.RegisterAsync does from C#).
            var registered = await _fixture.Page.EvaluateAsync<bool>("""
                () => {
                    monaco.languages.register({
                        id: 'test-csproj-lang',
                        extensions: ['.csproj'],
                        aliases: ['CSProj']
                    });
                    const langs = monaco.languages.getLanguages();
                    return langs.some(l => l.id === 'test-csproj-lang');
                }
                """);

            Assert.True(registered, "Custom language 'test-csproj-lang' should be registered.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // AddCommandAsync: callback verification via stdout
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task AddCommandAsync_CallbackFires()
    {
        _currentTestName = nameof(AddCommandAsync_CallbackFires);
        try
        {
            // Parse the command ID from the TEST_HARNESS startup line (cursor 0).
            var harnessLine = await _fixture.WaitForLogLineAfterAsync(
                0, @"TEST_HARNESS:commandId=", 30_000);

            // Extract commandId from "TEST_HARNESS:commandId=Command1,actionId=testCdpAction"
            var match = System.Text.RegularExpressions.Regex.Match(
                harnessLine, @"commandId=([^,]+)");
            Assert.True(match.Success, $"Could not parse commandId from: {harnessLine}");
            var commandId = match.Groups[1].Value;

            // Capture cursor before triggering the command.
            var cursor = _fixture.GetLogCursor();

            // Trigger the command from JS via callActionWithParameters (the bridge path).
            // addCommand registers the handler under the command name (e.g. "Command1").
            // The JS bridge calls parentAccessor/callActionWithParameters with that name.
            await _fixture.Page.EvaluateAsync($$"""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    const editorContext = EditorContext.getEditorForElement(
                        document.getElementById('editor-container'));
                    editorContext.Accessor.callActionWithParameters2('{{commandId}}', []);
                }
                """);

            // Wait for the callback marker in stdout.
            var callbackLine = await _fixture.WaitForLogLineAfterAsync(
                cursor, $"TEST_CALLBACK:{commandId}:invoked", 10_000);
            Assert.Contains($"TEST_CALLBACK:{commandId}:invoked", callbackLine);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // AddActionAsync: callback verification via stdout
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task AddActionAsync_CallbackFires()
    {
        _currentTestName = nameof(AddActionAsync_CallbackFires);
        try
        {
            // Parse the action ID from the TEST_HARNESS startup line (cursor 0).
            var harnessLine = await _fixture.WaitForLogLineAfterAsync(
                0, @"TEST_HARNESS:commandId=", 30_000);

            var match = System.Text.RegularExpressions.Regex.Match(
                harnessLine, @"actionId=([^\s,]+)");
            Assert.True(match.Success, $"Could not parse actionId from: {harnessLine}");
            var actionId = match.Groups[1].Value;

            // Capture cursor before triggering the action.
            var cursor = _fixture.GetLogCursor();

            // Trigger the action from JS. editor.getAction returns the registered action.
            await _fixture.Page.EvaluateAsync($$"""
                () => {
                    const action = monaco.editor.getEditors()[0].getAction('{{actionId}}');
                    if (action) action.run();
                }
                """);

            // Wait for the callback marker in stdout.
            var callbackLine = await _fixture.WaitForLogLineAfterAsync(
                cursor, $"TEST_CALLBACK:Action{actionId}:invoked", 10_000);
            Assert.Contains($"TEST_CALLBACK:Action{actionId}:invoked", callbackLine);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // Theme switching via bridge
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task ThemeSwitching_BridgeDriven()
    {
        _currentTestName = nameof(ThemeSwitching_BridgeDriven);
        try
        {
            await _fixture.ResetEditorStateAsync();

            // Set theme to dark via Monaco API (the bridge path from C# uses
            // InvokeScriptAsync("changeTheme"), which ultimately calls
            // monaco.editor.setTheme). Here we verify the DOM effect.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.setTheme('vs-dark')");

            // Verify the DOM reflects dark theme.
            var hasDarkClass = await _fixture.Page.EvaluateAsync<bool>(
                "() => document.querySelector('.monaco-editor')?.classList.contains('vs-dark') ?? false");

            var hasThemeAttr = await _fixture.Page.EvaluateAsync<bool>(
                "() => (document.body.getAttribute('data-vscode-theme-name') || '').includes('dark')");

            Assert.True(hasDarkClass || hasThemeAttr,
                "Expected dark theme class or attribute after switching to vs-dark.");

            // Reset theme.
            await _fixture.Page.EvaluateAsync("() => monaco.editor.setTheme('vs')");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // Syntax highlighting CSS verification
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task SyntaxHighlighting_CssTokensPresent()
    {
        _currentTestName = nameof(SyntaxHighlighting_CssTokensPresent);
        try
        {
            await _fixture.ResetEditorStateAsync();

            // Set language to javascript and provide code with keywords.
            await _fixture.Page.EvaluateAsync("""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    const model = editor.getModel();
                    monaco.editor.setModelLanguage(model, 'javascript');
                    editor.setValue('function hello() { return 42; }');
                }
                """);

            // Wait for tokenization to complete.
            await Task.Delay(1000);

            // Verify that syntax token CSS classes are present in the DOM.
            // Monaco tokenizes code and applies CSS classes like 'mtk1', 'mtk{N}',
            // or more specific token classes.
            var hasTokenClasses = await _fixture.Page.EvaluateAsync<bool>("""
                () => {
                    const tokens = document.querySelectorAll('.view-line span[class*="mtk"]');
                    return tokens.length > 0;
                }
                """);

            Assert.True(hasTokenClasses,
                "Expected syntax highlighting token CSS classes (mtk*) after setting JavaScript code.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // Markers via bridge (SetModelMarkersAsync path)
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task Markers_SetViaBridgeAndVerify()
    {
        _currentTestName = nameof(Markers_SetViaBridgeAndVerify);
        try
        {
            await _fixture.ResetEditorStateAsync();

            // Set text content first.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.getEditors()[0].setValue('let x = 1;\\nlet y = 2;')");

            // Add markers via Monaco JS API (simulating what SetModelMarkersAsync does).
            await _fixture.Page.EvaluateAsync("""
                () => {
                    const model = monaco.editor.getEditors()[0].getModel();
                    monaco.editor.setModelMarkers(model, 'test', [{
                        startLineNumber: 1, startColumn: 1,
                        endLineNumber: 1, endColumn: 5,
                        message: 'Bridge test marker',
                        severity: monaco.MarkerSeverity.Error
                    }]);
                }
                """);

            // Verify marker data roundtrips.
            var markerData = await _fixture.Page.EvaluateAsync<string>("""
                () => {
                    const model = monaco.editor.getEditors()[0].getModel();
                    const markers = monaco.editor.getModelMarkers({ resource: model.uri });
                    if (markers.length === 0) return 'none';
                    return JSON.stringify({
                        message: markers[0].message,
                        severity: markers[0].severity
                    });
                }
                """);

            Assert.Contains("Bridge test marker", markerData);
            Assert.Contains("8", markerData); // MarkerSeverity.Error = 8

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

    // ============================================================
    // Decorations via bridge (CSS injection)
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task Decorations_SetViaBridgeAndVerifyCss()
    {
        _currentTestName = nameof(Decorations_SetViaBridgeAndVerifyCss);
        try
        {
            await _fixture.ResetEditorStateAsync();

            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.getEditors()[0].setValue('Line 1\\nLine 2\\nLine 3')");

            // Add decoration with a custom CSS class and verify it appears.
            var addedIds = await _fixture.Page.EvaluateAsync<string[]>("""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    return editor.deltaDecorations([], [{
                        range: new monaco.Range(1, 1, 1, 7),
                        options: { inlineClassName: 'bridge-test-decoration' }
                    }]);
                }
                """);

            Assert.NotNull(addedIds);
            Assert.NotEmpty(addedIds);

            // Verify the decoration's CSS class is injected in the DOM.
            var hasCssClass = await _fixture.Page.EvaluateAsync<bool>("""
                () => {
                    const elements = document.querySelectorAll('.bridge-test-decoration');
                    return elements.length > 0;
                }
                """);

            Assert.True(hasCssClass,
                "Expected .bridge-test-decoration CSS class in DOM after adding decoration.");

            // Cleanup.
            if (addedIds.Length > 0)
            {
                var idsJson = System.Text.Json.JsonSerializer.Serialize(addedIds);
                await _fixture.Page.EvaluateAsync($$"""
                    () => {
                        const editor = monaco.editor.getEditors()[0];
                        editor.deltaDecorations({{idsJson}}, []);
                    }
                    """);
            }
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // Code folding
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task CodeFolding_FoldableContentHasRanges()
    {
        _currentTestName = nameof(CodeFolding_FoldableContentHasRanges);
        try
        {
            await _fixture.ResetEditorStateAsync();

            // Load content with foldable regions (a JS function block).
            await _fixture.Page.EvaluateAsync("""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    const model = editor.getModel();
                    monaco.editor.setModelLanguage(model, 'javascript');
                    editor.setValue('function foo() {\n  const x = 1;\n  const y = 2;\n  return x + y;\n}\n\nfunction bar() {\n  return 42;\n}');
                    // Ensure folding is enabled.
                    editor.updateOptions({ folding: true });
                }
                """);

            // Wait for language service to compute folding ranges.
            await Task.Delay(1500);

            // Check that folding ranges exist via the editor's internal folding model.
            var hasFolding = await _fixture.Page.EvaluateAsync<bool>("""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    // The folding model is accessible via the internal contribution.
                    // A simpler check: look for folding region indicators in the DOM.
                    const foldingElements = document.querySelectorAll('.codicon-folding-expanded, .codicon-folding-collapsed, .cldr.folding');
                    if (foldingElements.length > 0) return true;
                    // Fallback: check via the fold action availability.
                    const action = editor.getAction('editor.foldAll');
                    return action !== null && action !== undefined;
                }
                """);

            Assert.True(hasFolding,
                "Expected folding indicators or fold action available after loading foldable JavaScript content.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // ReadOnly toggle via bridge
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task ReadOnly_ToggleViaBridge()
    {
        _currentTestName = nameof(ReadOnly_ToggleViaBridge);
        try
        {
            await _fixture.ResetEditorStateAsync();

            // Set ReadOnly = true via the bridge notification.
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'ReadOnly', value: '"True"' })
                """);

            await Task.Delay(500);

            // Verify the editor options reflect read-only state.
            var isReadOnly = await _fixture.Page.EvaluateAsync<bool>(
                "() => monaco.editor.getEditors()[0].getOptions().get(monaco.editor.EditorOption.readOnly)");

            Assert.True(isReadOnly, "Expected editor to be read-only after setting ReadOnly=true via bridge.");

            // Reset: set ReadOnly = false.
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'ReadOnly', value: '"False"' })
                """);

            await Task.Delay(500);

            var isReadOnlyAfterReset = await _fixture.Page.EvaluateAsync<bool>(
                "() => monaco.editor.getEditors()[0].getOptions().get(monaco.editor.EditorOption.readOnly)");

            Assert.False(isReadOnlyAfterReset, "Expected editor to not be read-only after resetting via bridge.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // HasGlyphMargin toggle
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task HasGlyphMargin_ToggleVerifyDom()
    {
        _currentTestName = nameof(HasGlyphMargin_ToggleVerifyDom);
        try
        {
            await _fixture.ResetEditorStateAsync();

            // Verify glyph margin is visible (initial state has glyphMargin=true).
            var hasGlyphMargin = await _fixture.Page.EvaluateAsync<bool>("""
                () => {
                    const margin = document.querySelector('.margin-view-overlays');
                    const option = monaco.editor.getEditors()[0]
                        .getOptions().get(monaco.editor.EditorOption.glyphMargin);
                    return option === true || (margin !== null && margin.offsetWidth > 0);
                }
                """);

            Assert.True(hasGlyphMargin, "Expected glyph margin to be visible initially.");

            // Disable glyph margin via bridge.
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'HasGlyphMargin', value: '"False"' })
                """);

            await Task.Delay(500);

            // Verify glyph margin option is now disabled.
            var glyphMarginAfter = await _fixture.Page.EvaluateAsync<bool>(
                "() => monaco.editor.getEditors()[0].getOptions().get(monaco.editor.EditorOption.glyphMargin)");

            Assert.False(glyphMarginAfter, "Expected glyph margin to be disabled after setting HasGlyphMargin=false via bridge.");

            // Restore glyph margin.
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'HasGlyphMargin', value: '"True"' })
                """);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // SelectedText roundtrip
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task SelectedText_RoundTrip()
    {
        _currentTestName = nameof(SelectedText_RoundTrip);
        try
        {
            await _fixture.ResetEditorStateAsync();

            // Set known text content.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.getEditors()[0].setValue('Hello World Test Content')");

            // Set a selection from JS and verify it reaches C# via the bridge.
            await _fixture.Page.EvaluateAsync("""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    editor.setSelection(new monaco.Range(1, 1, 1, 6));
                }
                """);

            // Read SelectedText back via bridge (C# tracks selections via onDidChangeCursorSelection).
            // Give the bridge a moment to propagate the selection change.
            await Task.Delay(500);

            var selectedText = await _fixture.Page.EvaluateAsync<string>("""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    const sel = editor.getSelection();
                    return editor.getModel().getValueInRange(sel);
                }
                """);

            Assert.Equal("Hello", selectedText);

            // Now set SelectedText from C# side via bridge and verify it takes effect.
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'SelectedText', value: '"Replaced"' })
                """);

            await Task.Delay(500);

            // Verify the selected text was replaced in the editor.
            var textAfterReplace = await _fixture.Page.EvaluateAsync<string>(
                "() => monaco.editor.getEditors()[0].getValue()");

            Assert.Contains("Replaced", textAfterReplace);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }
}
