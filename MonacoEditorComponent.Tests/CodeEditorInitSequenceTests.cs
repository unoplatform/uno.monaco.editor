using Monaco;

using Xunit;

namespace MonacoEditorComponent.Tests;

public sealed class CodeEditorInitSequenceTests
{
    [Fact]
    public void ShouldInvokeDesktopBootstrap_LoadingNotInitializedAndNotInFlight_True()
    {
        var shouldInvoke = CodeEditor.ShouldInvokeDesktopBootstrap(
            EditorLifecycleState.Loading,
            initialized: false,
            bootstrapInFlight: false);

        Assert.True(shouldInvoke);
    }

    [Fact]
    public void ShouldInvokeDesktopBootstrap_LoadingButInFlight_False()
    {
        var shouldInvoke = CodeEditor.ShouldInvokeDesktopBootstrap(
            EditorLifecycleState.Loading,
            initialized: false,
            bootstrapInFlight: true);

        Assert.False(shouldInvoke);
    }

    [Fact]
    public void ShouldInvokeDesktopBootstrap_LoadedState_False()
    {
        var shouldInvoke = CodeEditor.ShouldInvokeDesktopBootstrap(
            EditorLifecycleState.Loaded,
            initialized: false,
            bootstrapInFlight: false);

        Assert.False(shouldInvoke);
    }

    [Fact]
    public void ShouldForwardPresenterFocus_LoadedInitializedFocusedWithView_True()
    {
        var shouldForward = CodeEditor.ShouldForwardPresenterFocus(
            new MockCodeEditorPresenter(),
            initialized: true,
            lifecycleState: EditorLifecycleState.Loaded,
            hostIsFocused: true);

        Assert.True(shouldForward);
    }

    [Fact]
    public void ShouldForwardPresenterFocus_NotLoaded_False()
    {
        var shouldForward = CodeEditor.ShouldForwardPresenterFocus(
            new MockCodeEditorPresenter(),
            initialized: true,
            lifecycleState: EditorLifecycleState.Loading,
            hostIsFocused: true);

        Assert.False(shouldForward);
    }

    [Fact]
    public void ShouldForwardPresenterFocus_NotHostFocused_False()
    {
        var shouldForward = CodeEditor.ShouldForwardPresenterFocus(
            new MockCodeEditorPresenter(),
            initialized: true,
            lifecycleState: EditorLifecycleState.Loaded,
            hostIsFocused: false);

        Assert.False(shouldForward);
    }

    [Fact]
    public void ShouldForwardPresenterFocus_NullView_False()
    {
        var shouldForward = CodeEditor.ShouldForwardPresenterFocus(
            view: null,
            initialized: true,
            lifecycleState: EditorLifecycleState.Loaded,
            hostIsFocused: true);

        Assert.False(shouldForward);
    }

    [Theory]
    [InlineData(true, true, 2, true)]   // Loaded and visible
    [InlineData(true, false, 2, false)] // Control unloaded
    [InlineData(false, true, 2, false)] // Editor not initialized
    [InlineData(true, true, 1, false)]  // Loading lifecycle
    public void ShouldPresenterBeVisible_GuardsExpectedCases(
        bool isEditorLoaded,
        bool isControlLoaded,
        int lifecycleState,
        bool expected)
    {
        var shouldBeVisible = CodeEditor.ShouldPresenterBeVisible(
            isEditorLoaded,
            isControlLoaded,
            (EditorLifecycleState)lifecycleState);

        Assert.Equal(expected, shouldBeVisible);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldKeepHostVisibleWhenHidden_GuardsExpectedCases(
        bool isWindows,
        bool isCoreWebView2Initialized,
        bool expected)
    {
        var keepVisible = DesktopCodeEditorPresenter.ShouldKeepHostVisibleWhenHidden(
            isWindows,
            isCoreWebView2Initialized);

        Assert.Equal(expected, keepVisible);
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void ShouldHostBeVisible_GuardsExpectedCases(
        bool forceVisibleForInitialization,
        bool isHostVisibleRequested,
        bool keepHostVisibleWhenHidden,
        bool expected)
    {
        var shouldBeVisible = DesktopCodeEditorPresenter.ShouldHostBeVisible(
            forceVisibleForInitialization,
            isHostVisibleRequested,
            keepHostVisibleWhenHidden);

        Assert.Equal(expected, shouldBeVisible);
    }

    [Fact]
    public void BuildCreateMonacoEditorScript_EmbedsInitialStateAsJsonStringLiteral()
    {
        const string initialStateJson = "{\"requestedTheme\":0,\"themeName\":\"Light\",\"isHighContrast\":false,\"text\":\"abc\",\"language\":\"plaintext\",\"readOnly\":false}";
        var escapedState = System.Text.Json.JsonSerializer.Serialize(initialStateJson);
        var script = CodeEditor.BuildCreateMonacoEditorScript(escapedState);

        Assert.Contains("createMonacoEditor", script);
        Assert.Contains(escapedState, script);
    }
}
