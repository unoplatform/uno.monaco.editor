using Monaco;
using Monaco.Extensions;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Regression tests verifying that <see cref="ICodeEditorPresenterExtensions"/>
/// routes calls through <see cref="ICodeEditorPresenter.InvokeMethodAsync"/> and
/// <see cref="ICodeEditorPresenter.InvokeScriptWithElementAsync"/> instead of
/// building raw scripts that reference an undefined <c>element</c> variable.
///
/// These tests pin the routing change that enables desktop platform support for
/// AddActionAsync / AddCommandAsync and all other interop methods.
/// </summary>
public sealed class PresenterRoutingTests
{
    private readonly MockCodeEditorPresenter _presenter = new();

    [Fact]
    public async Task InvokeScriptAsync_WithMethodAndArgs_RoutesViaInvokeMethodAsync()
    {
        // InvokeScriptAsync<T>(method, args) must route through InvokeMethodAsync,
        // NOT build a "method(element, ...)" script string for InvokeScriptAsync.
        await _presenter.InvokeScriptAsync("updateContent", "\"hello\"", serialize: false);

        // InvokeMethodAsync should have been called with the method name and args.
        Assert.Single(_presenter.InvokeMethodCalls);
        var (method, args) = _presenter.InvokeMethodCalls[0];
        Assert.Equal("updateContent", method);
        Assert.Contains("\"hello\"", args);

        // The old InvokeScriptAsync(string script) must NOT have been called.
        Assert.Empty(_presenter.InvokeScriptCalls);

        // InvokeScriptWithElementAsync must NOT have been called.
        Assert.Empty(_presenter.InvokeScriptWithElementCalls);
    }

    [Fact]
    public async Task InvokeScriptAsync_WithMultipleArgs_RoutesViaInvokeMethodAsync()
    {
        await _presenter.InvokeScriptAsync("changeTheme", ["\"vs-dark\"", "false"], serialize: false);

        Assert.Single(_presenter.InvokeMethodCalls);
        var (method, args) = _presenter.InvokeMethodCalls[0];
        Assert.Equal("changeTheme", method);
        Assert.Equal(2, args.Length);
        Assert.Equal("\"vs-dark\"", args[0]);
        Assert.Equal("false", args[1]);
    }

    [Fact]
    public async Task RunScriptAsync_RoutesViaInvokeScriptWithElementAsync()
    {
        // RunScriptAsync must route through InvokeScriptWithElementAsync,
        // NOT through the old InvokeScriptAsync(string script) path that
        // does not define the `element` variable on desktop.
        await _presenter.RunScriptAsync("EditorContext.getEditorForElement(element).editor.focus();");

        Assert.Single(_presenter.InvokeScriptWithElementCalls);
        Assert.Contains("focus", _presenter.InvokeScriptWithElementCalls[0]);

        // InvokeMethodAsync must NOT have been called.
        Assert.Empty(_presenter.InvokeMethodCalls);

        // The old InvokeScriptAsync(string) must NOT have been called.
        Assert.Empty(_presenter.InvokeScriptCalls);
    }

    [Fact]
    public async Task InvokeScriptAsync_WithSingleArg_RoutesViaInvokeMethodAsync()
    {
        // Single-arg overload: InvokeScriptAsync(method, arg) should also
        // route through InvokeMethodAsync (via the array overload).
        await _presenter.InvokeScriptAsync("updateLanguage", "csharp");

        Assert.Single(_presenter.InvokeMethodCalls);
        var (method, _) = _presenter.InvokeMethodCalls[0];
        Assert.Equal("updateLanguage", method);

        Assert.Empty(_presenter.InvokeScriptCalls);
        Assert.Empty(_presenter.InvokeScriptWithElementCalls);
    }

    [Fact]
    public async Task RunScriptAsync_ReturnsDeserializedResult()
    {
        // RunScriptAsync<T> should return default(T) for "null" return from mock.
        var result = await _presenter.RunScriptAsync<string>("return 'test';");

        Assert.Null(result);
        Assert.Single(_presenter.InvokeScriptWithElementCalls);
    }
}
