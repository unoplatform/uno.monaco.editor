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
}
