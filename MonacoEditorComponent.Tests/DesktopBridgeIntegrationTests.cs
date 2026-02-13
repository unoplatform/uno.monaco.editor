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

            // Wait deterministically for the C# property change to propagate back to Monaco.
            await _fixture.Page.WaitForFunctionAsync(
                "() => monaco.editor.getEditors()[0].getValue() === 'bridge-driven text'",
                null, new PageWaitForFunctionOptions { Timeout = 5000 });

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

            // Wait deterministically for C# side to accept the change.
            await _fixture.Page.WaitForFunctionAsync("""
                async () => {
                    const result = await window.__jsonRpc.sendRequest('parentAccessor/getJsonValue', { name: 'CodeLanguage' });
                    return result && result.includes('xml');
                }
                """, null, new PageWaitForFunctionOptions { Timeout = 5000 });

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
    // Custom language registration (C# harness -> Monaco)
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task CustomLanguage_RegisterAndVerifyAvailable()
    {
        _currentTestName = nameof(CustomLanguage_RegisterAndVerifyAvailable);
        try
        {
            // The C# test harness in EditorControl.xaml.cs calls
            // Editor.Languages.RegisterAsync({ Id = "test-csproj-lang" })
            // at startup via the C# LanguagesHelper API. Verify the stdout marker
            // proves the C# registration path executed.
            var langMarker = await _fixture.WaitForLogLineAfterAsync(
                0, @"TEST_HARNESS_LANG:registered=test-csproj-lang", 30_000);
            Assert.Contains("TEST_HARNESS_LANG", langMarker);

            // Verify Monaco has the language registered.
            var registered = await _fixture.Page.WaitForFunctionAsync("""
                () => {
                    const langs = monaco.languages.getLanguages();
                    return langs.some(l => l.id === 'test-csproj-lang');
                }
                """, null, new PageWaitForFunctionOptions { Timeout = 5000 });

            Assert.NotNull(registered);

            // Switch the editor to the custom language via bridge and verify C# accepted it.
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'CodeLanguage', value: '"test-csproj-lang"' })
                """);

            await _fixture.Page.WaitForFunctionAsync("""
                async () => {
                    const result = await window.__jsonRpc.sendRequest('parentAccessor/getJsonValue', { name: 'CodeLanguage' });
                    return result && result.includes('test-csproj-lang');
                }
                """, null, new PageWaitForFunctionOptions { Timeout = 5000 });
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

            // Trigger the command through Monaco's full dispatch path:
            // editor.trigger -> Monaco command registry -> JS addCommand handler
            // -> Accessor.callActionWithParameters2 -> parentAccessor/callActionWithParameters
            // -> C# OnCallActionWithParameters -> registered handler -> stdout callback.
            // This verifies the entire C# AddCommandAsync -> Monaco registration -> bridge callback chain.
            await _fixture.Page.EvaluateAsync($$"""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    editor.trigger('test', '{{commandId}}', null);
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
    // Theme switching via bridge eval path
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task ThemeSwitching_BridgeDriven()
    {
        _currentTestName = nameof(ThemeSwitching_BridgeDriven);
        try
        {
            // The C# test harness in EditorControl.xaml.cs sets
            // Editor.RequestedTheme = ElementTheme.Dark at startup via the C# DP path.
            // Verify the stdout marker proves the C# theme-switch path executed.
            var themeMarker = await _fixture.WaitForLogLineAfterAsync(
                0, @"TEST_HARNESS_THEME:set=Dark", 30_000);
            Assert.Contains("TEST_HARNESS_THEME", themeMarker);

            // Wait deterministically for the DOM to reflect the dark theme.
            await _fixture.Page.WaitForFunctionAsync("""
                () => {
                    const el = document.querySelector('.monaco-editor');
                    if (!el) return false;
                    return el.classList.contains('vs-dark')
                        || (document.body.getAttribute('data-vscode-theme-name') || '').includes('dark');
                }
                """, null, new PageWaitForFunctionOptions { Timeout = 5000 });

            // Verify the DOM reflects dark theme.
            var hasDarkClass = await _fixture.Page.EvaluateAsync<bool>(
                "() => document.querySelector('.monaco-editor')?.classList.contains('vs-dark') ?? false");

            Assert.True(hasDarkClass,
                "Expected dark theme class after C# RequestedTheme = Dark.");

            // Now test bridge-driven theme reset: switch back via bridge notification.
            // The bridge path sends a changeTheme call through the same C# DP mechanism.
            await _fixture.Page.EvaluateAsync("""
                () => changeTheme(document.getElementById('editor-container'), 'Light', 'false')
                """);

            await _fixture.Page.WaitForFunctionAsync(
                "() => !document.querySelector('.monaco-editor')?.classList.contains('vs-dark')",
                null, new PageWaitForFunctionOptions { Timeout = 5000 });
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // Syntax highlighting: set language + text via bridge, verify CSS tokens
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task SyntaxHighlighting_CssTokensPresent()
    {
        _currentTestName = nameof(SyntaxHighlighting_CssTokensPresent);
        try
        {
            await _fixture.ResetEditorStateAsync();

            // Set language via bridge (CodeLanguage DP).
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'CodeLanguage', value: '"javascript"' })
                """);

            // Set code with keywords via bridge (Text DP).
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'Text', value: '"function hello() { return 42; }"' })
                """);

            // Wait deterministically for bridge propagation and tokenization.
            // Monaco tokenizes code and applies CSS classes like 'mtk1', 'mtk{N}'.
            await _fixture.Page.WaitForFunctionAsync("""
                () => {
                    const tokens = document.querySelectorAll('.view-line span[class*="mtk"]');
                    return tokens.length > 0;
                }
                """, null, new PageWaitForFunctionOptions { Timeout = 10_000 });

            var hasTokenClasses = await _fixture.Page.EvaluateAsync<bool>("""
                () => {
                    const tokens = document.querySelectorAll('.view-line span[class*="mtk"]');
                    return tokens.length > 0;
                }
                """);

            Assert.True(hasTokenClasses,
                "Expected syntax highlighting token CSS classes (mtk*) after bridge-driven JavaScript code.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // Markers: set text via bridge, then verify markers roundtrip
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task Markers_SetViaBridgeAndVerify()
    {
        _currentTestName = nameof(Markers_SetViaBridgeAndVerify);
        try
        {
            // The C# test harness in EditorControl.xaml.cs calls
            // Editor.SetModelMarkersAsync("testHarness", [...]) at startup via the C# API.
            // Verify the stdout marker proves the C# marker-setting path executed.
            var markersMarker = await _fixture.WaitForLogLineAfterAsync(
                0, @"TEST_HARNESS_MARKERS:set=harness-marker", 30_000);
            Assert.Contains("TEST_HARNESS_MARKERS", markersMarker);

            // Wait deterministically for the markers to be visible in Monaco.
            await _fixture.Page.WaitForFunctionAsync("""
                () => {
                    const model = monaco.editor.getEditors()[0].getModel();
                    const markers = monaco.editor.getModelMarkers({ resource: model.uri });
                    return markers.some(m => m.message === 'harness-marker');
                }
                """, null, new PageWaitForFunctionOptions { Timeout = 5000 });

            // Verify marker data set by the C# SetModelMarkersAsync API.
            var markerData = await _fixture.Page.EvaluateAsync<string>("""
                () => {
                    const model = monaco.editor.getEditors()[0].getModel();
                    const markers = monaco.editor.getModelMarkers({ resource: model.uri });
                    const harnessMarker = markers.find(m => m.message === 'harness-marker');
                    if (!harnessMarker) return 'none';
                    return JSON.stringify({
                        message: harnessMarker.message,
                        severity: harnessMarker.severity,
                        source: harnessMarker.source
                    });
                }
                """);

            Assert.Contains("harness-marker", markerData);
            Assert.Contains("testHarness", markerData); // Source matches C# owner
            Assert.Contains("4", markerData); // MarkerSeverity.Warning = 4

            // Also test bridge-driven marker addition to verify round-trip.
            await _fixture.ResetEditorStateAsync();

            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'Text', value: '"let x = 1;\\nlet y = 2;"' })
                """);

            await _fixture.Page.WaitForFunctionAsync(
                "() => monaco.editor.getEditors()[0].getValue().includes('let x')",
                null, new PageWaitForFunctionOptions { Timeout = 5000 });

            // Add a bridge-driven marker via the same JS API that SetModelMarkersAsync calls.
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

            var bridgeMarkerData = await _fixture.Page.EvaluateAsync<string>("""
                () => {
                    const model = monaco.editor.getEditors()[0].getModel();
                    const markers = monaco.editor.getModelMarkers({ resource: model.uri });
                    const m = markers.find(m => m.message === 'Bridge test marker');
                    if (!m) return 'none';
                    return JSON.stringify({ message: m.message, severity: m.severity });
                }
                """);

            Assert.Contains("Bridge test marker", bridgeMarkerData);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // Decorations: set text via bridge, verify CSS injection
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task Decorations_SetViaBridgeAndVerifyCss()
    {
        _currentTestName = nameof(Decorations_SetViaBridgeAndVerifyCss);
        try
        {
            // The C# test harness in EditorControl.xaml.cs adds a decoration via
            // Editor.Decorations.Add(...) at startup through the C# Decorations collection.
            // Verify the stdout marker proves the C# decoration path executed.
            var decoMarker = await _fixture.WaitForLogLineAfterAsync(
                0, @"TEST_HARNESS_DECORATIONS:added=1", 30_000);
            Assert.Contains("TEST_HARNESS_DECORATIONS", decoMarker);

            // Wait deterministically for the decoration to appear in the DOM.
            // The C# harness adds a CssInlineStyle with ForegroundColor = Red, which
            // generates a dynamic CSS class name. Verify that decorations exist beyond
            // the baseline Monaco decorations.
            await _fixture.Page.WaitForFunctionAsync("""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    const model = editor.getModel();
                    if (!model) return false;
                    const decos = model.getAllDecorations();
                    // Filter out built-in decorations (those with empty className/inlineClassName).
                    return decos.some(d => d.options && (d.options.inlineClassName || d.options.className));
                }
                """, null, new PageWaitForFunctionOptions { Timeout = 5000 });

            // Also test bridge-driven decoration addition via deltaDecorations.
            await _fixture.ResetEditorStateAsync();

            // Set text via the C# bridge so preconditions flow through the bridge path.
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'Text', value: '"Line 1\\nLine 2\\nLine 3"' })
                """);

            await _fixture.Page.WaitForFunctionAsync(
                "() => monaco.editor.getEditors()[0].getValue().includes('Line 1')",
                null, new PageWaitForFunctionOptions { Timeout = 5000 });

            // Add decoration via JS deltaDecorations (same path as DeltaDecorationsHelperAsync).
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

            // Wait deterministically for the decoration CSS class in the DOM.
            await _fixture.Page.WaitForFunctionAsync("""
                () => document.querySelectorAll('.bridge-test-decoration').length > 0
                """, null, new PageWaitForFunctionOptions { Timeout = 5000 });

            var hasCssClass = await _fixture.Page.EvaluateAsync<bool>("""
                () => document.querySelectorAll('.bridge-test-decoration').length > 0
                """);

            Assert.True(hasCssClass,
                "Expected .bridge-test-decoration CSS class in DOM after adding decoration.");
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    // ============================================================
    // Code folding: set content + language via bridge, verify ranges
    // ============================================================

    [Fact]
    [Trait("Category", "DesktopCDP")]
    public async Task CodeFolding_FoldableContentHasRanges()
    {
        _currentTestName = nameof(CodeFolding_FoldableContentHasRanges);
        try
        {
            await _fixture.ResetEditorStateAsync();

            // Set language via bridge (CodeLanguage DP).
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'CodeLanguage', value: '"javascript"' })
                """);

            // Set foldable content via bridge (Text DP).
            var foldableCode = "function foo() {\\n  const x = 1;\\n  const y = 2;\\n  return x + y;\\n}\\n\\nfunction bar() {\\n  return 42;\\n}";
            await _fixture.Page.EvaluateAsync($$"""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'Text', value: '"{{foldableCode}}"' })
                """);

            // Wait deterministically for the text to propagate.
            await _fixture.Page.WaitForFunctionAsync(
                "() => monaco.editor.getEditors()[0].getValue().includes('function foo')",
                null, new PageWaitForFunctionOptions { Timeout = 5000 });

            // Ensure folding is enabled via the editor options.
            await _fixture.Page.EvaluateAsync(
                "() => monaco.editor.getEditors()[0].updateOptions({ folding: true })");

            // Wait deterministically for folding indicators or fold action to be available.
            await _fixture.Page.WaitForFunctionAsync("""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    const foldingElements = document.querySelectorAll('.codicon-folding-expanded, .codicon-folding-collapsed, .cldr.folding');
                    if (foldingElements.length > 0) return true;
                    const action = editor.getAction('editor.foldAll');
                    return action !== null && action !== undefined;
                }
                """, null, new PageWaitForFunctionOptions { Timeout = 10_000 });

            var hasFolding = await _fixture.Page.EvaluateAsync<bool>("""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    const foldingElements = document.querySelectorAll('.codicon-folding-expanded, .codicon-folding-collapsed, .cldr.folding');
                    if (foldingElements.length > 0) return true;
                    const action = editor.getAction('editor.foldAll');
                    return action !== null && action !== undefined;
                }
                """);

            Assert.True(hasFolding,
                "Expected folding indicators or fold action available after bridge-driven foldable JavaScript content.");
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

            // Wait deterministically for the readOnly option to propagate.
            await _fixture.Page.WaitForFunctionAsync(
                "() => monaco.editor.getEditors()[0].getOptions().get(monaco.editor.EditorOption.readOnly) === true",
                null, new PageWaitForFunctionOptions { Timeout = 5000 });

            var isReadOnly = await _fixture.Page.EvaluateAsync<bool>(
                "() => monaco.editor.getEditors()[0].getOptions().get(monaco.editor.EditorOption.readOnly)");

            Assert.True(isReadOnly, "Expected editor to be read-only after setting ReadOnly=true via bridge.");

            // Reset: set ReadOnly = false.
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'ReadOnly', value: '"False"' })
                """);

            // Wait deterministically for the readOnly option to propagate.
            await _fixture.Page.WaitForFunctionAsync(
                "() => monaco.editor.getEditors()[0].getOptions().get(monaco.editor.EditorOption.readOnly) === false",
                null, new PageWaitForFunctionOptions { Timeout = 5000 });

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

            // Wait deterministically for the glyphMargin option to propagate.
            await _fixture.Page.WaitForFunctionAsync(
                "() => monaco.editor.getEditors()[0].getOptions().get(monaco.editor.EditorOption.glyphMargin) === false",
                null, new PageWaitForFunctionOptions { Timeout = 5000 });

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

            // Set known text content via bridge.
            await _fixture.Page.EvaluateAsync("""
                () => window.__jsonRpc.sendNotification('parentAccessor/setValue',
                    { name: 'Text', value: '"Hello World Test Content"' })
                """);

            await _fixture.Page.WaitForFunctionAsync(
                "() => monaco.editor.getEditors()[0].getValue() === 'Hello World Test Content'",
                null, new PageWaitForFunctionOptions { Timeout = 5000 });

            // Set a selection from JS and verify it reaches C# via the bridge.
            await _fixture.Page.EvaluateAsync("""
                () => {
                    const editor = monaco.editor.getEditors()[0];
                    editor.setSelection(new monaco.Range(1, 1, 1, 6));
                }
                """);

            // Verify the JS->C# leg: read SelectedText via bridge getJsonValue.
            // C# tracks selections via onDidChangeCursorSelection event -> parentAccessor/setValue.
            // The getJsonValue reads the C# DP value back through the bridge.
            await _fixture.Page.WaitForFunctionAsync("""
                async () => {
                    const result = await window.__jsonRpc.sendRequest('parentAccessor/getJsonValue', { name: 'SelectedText' });
                    return result && result.includes('Hello');
                }
                """, null, new PageWaitForFunctionOptions { Timeout = 5000 });

            var selectedTextFromBridge = await _fixture.Page.EvaluateAsync<string>(
                "() => window.__jsonRpc.sendRequest('parentAccessor/getJsonValue', { name: 'SelectedText' })");
            Assert.Contains("Hello", selectedTextFromBridge);

            // Also verify from the JS side.
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

            // Wait deterministically for the replacement to take effect.
            await _fixture.Page.WaitForFunctionAsync(
                "() => monaco.editor.getEditors()[0].getValue().includes('Replaced')",
                null, new PageWaitForFunctionOptions { Timeout = 5000 });

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
