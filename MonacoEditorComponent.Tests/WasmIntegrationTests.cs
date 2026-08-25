using Microsoft.Playwright;

using Monaco.Editor;

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
    /// The sample app registers a document-color provider that searches the model for 8-digit hex
    /// literals. <c>Content.txt</c> has none, so the search must come back empty. It did not: the
    /// script was invoked as <c>findMatches(element, ...)</c>, which shifted every argument by one
    /// and left Monaco compiling the stringified DOM element into a regex. Running the real script
    /// against the real model is the only way to catch that -- the C# side happily deserializes the
    /// bogus match either way.
    /// </summary>
    [Fact]
    [Trait("Category", "WasmPlaywright")]
    public async Task FindMatches_ScriptSentByModelHelperMatchesNothingWhenThePatternIsAbsent()
    {
        _currentTestName = nameof(FindMatches_ScriptSentByModelHelperMatchesNothingWhenThePatternIsAbsent);
        try
        {
            var script = ModelHelper.BuildFindMatchesScript(
                searchString: "#[A-Fa-f0-9]{8}",
                searchOnlyEditableRange: true,
                isRegex: true,
                matchCase: true,
                wordSeparators: null,
                captureMatches: true,
                limitResultCount: 999);

            // Mirrors the WASM host, which evals the script with `element` bound to the editor's div.
            var matches = await _fixture.Page.EvaluateAsync<string>(
                """
                (s) => {
                    const element = [...EditorContext._editors.keys()][0];
                    return JSON.stringify(eval(s));
                }
                """,
                script);

            Assert.Equal("[]", matches);
        }
        catch
        {
            _testFailed = true;
            throw;
        }
    }

    /// <summary>
    /// Provider registrations live on the page-global <c>monaco.languages</c> registry, which every
    /// editor on a WASM page shares. Each new editor used to add its own document-color provider and
    /// Monaco concatenated all of their results, so the same color produced one stacked colorpicker
    /// decoration per open editor. Registering repeatedly must leave exactly one live provider.
    /// </summary>
    [Fact]
    [Trait("Category", "WasmPlaywright")]
    public async Task ColorProvider_RepeatedRegistrationKeepsASingleProvider()
    {
        _currentTestName = nameof(ColorProvider_RepeatedRegistrationKeepsASingleProvider);
        try
        {
            var roundTrips = await _fixture.Page.EvaluateAsync<int>(
                """
                async () => {
                    const element = [...EditorContext._editors.keys()][0];
                    const context = EditorContext.getEditorForElement(element);
                    const accessor = context.Accessor;
                    const original = accessor.callEvent.bind(accessor);
                    let calls = 0;
                    accessor.callEvent = (name, p1, p2) => {
                        if (String(name).startsWith('ProvideDocumentColors')) { calls++; }
                        return original(name, p1, p2);
                    };

                    try {
                        // Stand in for three more editors loading and registering the same provider.
                        globalThis.registerColorProvider(element, 'csharp');
                        globalThis.registerColorProvider(element, 'csharp');
                        globalThis.registerColorProvider(element, 'csharp');
                        await new Promise(r => setTimeout(r, 2000));

                        // Force exactly one color computation and count the managed round-trips.
                        calls = 0;
                        const model = context.model;
                        const value = model.getValue();
                        model.setValue(value + String.fromCharCode(10));
                        await new Promise(r => setTimeout(r, 2000));
                        model.setValue(value);
                        return calls;
                    } finally {
                        accessor.callEvent = original;
                    }
                }
                """);

            Assert.Equal(1, roundTrips);
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
