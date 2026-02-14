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

    [Theory]
    [InlineData(false, false, 0, true)]
    [InlineData(true, false, 0, false)]
    [InlineData(false, true, 0, false)]
    [InlineData(false, false, 1, false)]
    [InlineData(false, false, 2, false)]
    public void ShouldStartDesktopLaunchOnControlLoaded_GuardsExpectedCases(
        bool isCoreWebView2Initialized,
        bool isLaunchInProgress,
        int lifecycleState,
        bool expected)
    {
        var shouldStart = CodeEditor.ShouldStartDesktopLaunchOnControlLoaded(
            isCoreWebView2Initialized,
            isLaunchInProgress,
            (EditorLifecycleState)lifecycleState);

        Assert.Equal(expected, shouldStart);
    }

    [Theory]
    [InlineData(0, false, true, false, false, true)]
    [InlineData(1, false, true, false, false, false)]
    [InlineData(0, true, true, false, false, false)]
    [InlineData(0, false, false, false, false, false)]
    [InlineData(0, false, true, true, false, false)]
    [InlineData(0, false, true, false, true, false)]
    public void ShouldRestoreDesktopBridgeOnControlLoaded_GuardsExpectedCases(
        int lifecycleState,
        bool hasInitializedPresenter,
        bool isCoreWebView2Initialized,
        bool isLaunchInProgress,
        bool bootstrapInFlight,
        bool expected)
    {
        var shouldRestore = CodeEditor.ShouldRestoreDesktopBridgeOnControlLoaded(
            (EditorLifecycleState)lifecycleState,
            hasInitializedPresenter,
            isCoreWebView2Initialized,
            isLaunchInProgress,
            bootstrapInFlight);

        Assert.Equal(expected, shouldRestore);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    public void ShouldDeferDesktopBootstrapOnNavigationCompleted_GuardsExpectedCases(
        bool controlIsLoaded,
        bool navigationSucceeded,
        bool canInvokeBootstrap,
        bool expected)
    {
        var shouldDefer = CodeEditor.ShouldDeferDesktopBootstrapOnNavigationCompleted(
            controlIsLoaded,
            navigationSucceeded,
            canInvokeBootstrap);

        Assert.Equal(expected, shouldDefer);
    }

    [Theory]
    [InlineData(true, true, false, true, true)]
    [InlineData(false, true, false, true, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, true, true, true, false)]
    [InlineData(true, true, false, false, false)]
    public void ShouldResumeDeferredDesktopBootstrapOnControlLoaded_GuardsExpectedCases(
        bool hasPendingBootstrap,
        bool isCoreWebView2Initialized,
        bool isLaunchInProgress,
        bool canInvokeBootstrap,
        bool expected)
    {
        var shouldResume = CodeEditor.ShouldResumeDeferredDesktopBootstrapOnControlLoaded(
            hasPendingBootstrap,
            isCoreWebView2Initialized,
            isLaunchInProgress,
            canInvokeBootstrap);

        Assert.Equal(expected, shouldResume);
    }

    [Theory]
    [InlineData(true, 1, false, true)]   // Loading
    [InlineData(true, 2, true, true)]    // Loaded + rebootstrap in flight
    [InlineData(true, 2, false, false)]  // Loaded without rebootstrap
    [InlineData(true, 0, true, false)]   // Unloaded
    [InlineData(false, 1, true, false)]  // Not loaded control
    public void ShouldProcessCodeEditorLoaded_GuardsExpectedCases(
        bool isLoaded,
        int lifecycleState,
        bool bootstrapInFlight,
        bool expected)
    {
        var shouldProcess = CodeEditor.ShouldProcessCodeEditorLoaded(
            isLoaded,
            (EditorLifecycleState)lifecycleState,
            bootstrapInFlight);

        Assert.Equal(expected, shouldProcess);
    }

    [Theory]
    [InlineData("{\"initComplete\":true}", true)]
    [InlineData("{\"initComplete\":false}", false)]
    [InlineData("\"{\\\"initComplete\\\":true}\"", true)]
    [InlineData("\"{\\\"initComplete\\\":false}\"", false)]
    [InlineData("\"{\\\"initComplete\\\":\\\"true\\\"}\"", true)]
    [InlineData("\"{\\\"other\\\":1,\\\"initComplete\\\":true}\"", true)]
    public void RuntimeSnapshotIndicatesInitComplete_ParsesDirectAndEscapedJson(
        string runtimeSnapshot,
        bool expected)
    {
        var isComplete = CodeEditor.RuntimeSnapshotIndicatesInitComplete(runtimeSnapshot);

        Assert.Equal(expected, isComplete);
    }
}
