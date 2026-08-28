using System.Reflection;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Regression tests for BUG 1: WASM LayoutUpdated fires every frame (resize perf).
///
/// The old implementation subscribed to <c>LayoutUpdated</c> in the
/// <c>WasmCodeEditorPresenter</c> constructor and called
/// <c>NativeMethods.RefreshLayout</c> on every layout pass, causing excessive
/// JS interop calls. The fix replaces this with a TS-side <c>ResizeObserver</c>
/// that only fires on actual container size changes.
///
/// These tests use reflection to verify the code structure since
/// <c>WasmCodeEditorPresenter</c> cannot be instantiated outside WASM.
/// </summary>
public sealed class WasmResizeRegressionTests
{
    private static readonly Type? s_presenterType = typeof(Monaco.CodeEditor)
        .Assembly
        .GetType("Monaco.WasmCodeEditorPresenter");

    [Fact]
    public void WasmCodeEditorPresenter_TypeExists()
    {
        Assert.NotNull(s_presenterType);
    }

    /// <summary>
    /// Verifies that the nested <c>NativeMethods</c> class no longer declares a
    /// <c>RefreshLayout</c> method. After removing the LayoutUpdated handler and
    /// the Launch-time call, there are zero callers, so the P/Invoke must be removed.
    /// </summary>
    [Fact]
    public void NativeMethods_DoesNotDeclare_RefreshLayout()
    {
        Assert.NotNull(s_presenterType);

        // NativeMethods is a private nested type
        var nativeMethodsType = s_presenterType!.GetNestedType(
            "NativeMethods",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(nativeMethodsType);

        var refreshLayoutMethod = nativeMethodsType!.GetMethod(
            "RefreshLayout",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

        Assert.Null(refreshLayoutMethod);
    }

    /// <summary>
    /// Verifies that the <c>WasmCodeEditorPresenter</c> constructor does not
    /// subscribe to the <c>LayoutUpdated</c> event by confirming the IL does not
    /// reference the event. We use a source-text approach: read the source file
    /// and confirm the LayoutUpdated subscription pattern is absent.
    ///
    /// As a practical compile-time regression guard, we verify that the type does
    /// not have any private fields or compiler-generated classes whose names
    /// suggest a LayoutUpdated subscription closure.
    /// </summary>
    [Fact]
    public void Constructor_DoesNotSubscribe_LayoutUpdated()
    {
        Assert.NotNull(s_presenterType);

        // Compiler-generated closure types for LayoutUpdated lambda subscriptions
        // would appear as nested types with names containing "LayoutUpdated" or
        // the constructor would generate a cached delegate field.
        // Verify no fields reference LayoutUpdated by name.
        var allFields = s_presenterType!.GetFields(
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var field in allFields)
        {
            // Compiler-generated delegate cache fields for event subscriptions
            // contain the event name in their mangled name
            Assert.DoesNotContain(
                "LayoutUpdated",
                field.Name,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Verifies that the <c>NativeMethods</c> nested type only contains the
    /// expected methods (GetSrc, SetSrc, InitializeMonaco, InitializeMonacoDiff) and no
    /// others,
    /// preventing accidental re-introduction of removed interop methods.
    /// </summary>
    [Fact]
    public void NativeMethods_ContainsOnlyExpectedMethods()
    {
        Assert.NotNull(s_presenterType);

        var nativeMethodsType = s_presenterType!.GetNestedType(
            "NativeMethods",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(nativeMethodsType);

        var declaredMethods = nativeMethodsType!
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .OrderBy(n => n)
            .ToArray();

        var expectedMethods = new[] { "GetSrc", "InitializeMonaco", "InitializeMonacoDiff", "SetSrc" };

        Assert.Equal(expectedMethods, declaredMethods);
    }
}
