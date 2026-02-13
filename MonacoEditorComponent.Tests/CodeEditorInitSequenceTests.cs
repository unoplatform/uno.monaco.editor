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

    [Fact]
    public void BuildCreateMonacoEditorScript_EmbedsInitialStateAsJsonStringLiteral()
    {
        const string initialStateJson = "{\"requestedTheme\":0,\"themeName\":\"Light\",\"isHighContrast\":false,\"text\":\"abc\",\"language\":\"plaintext\",\"readOnly\":false}";
        var escapedState = System.Text.Json.JsonSerializer.Serialize(initialStateJson);
        var script = CodeEditor.BuildCreateMonacoEditorScript(escapedState);

        Assert.Contains("createMonacoEditor", script);
        Assert.Contains(escapedState, script);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void ShouldPreserveDesktopPresenterOnDeferredUnload_GuardsExpectedCases(
        bool isLoaded,
        bool hasHealthyDesktopPresenter,
        bool expected)
    {
        var preserve = CodeEditor.ShouldPreserveDesktopPresenterOnDeferredUnload(
            isLoaded,
            hasHealthyDesktopPresenter);

        Assert.Equal(expected, preserve);
    }
}
