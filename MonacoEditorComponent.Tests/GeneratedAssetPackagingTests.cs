using System.Reflection;

using Monaco;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Guards the packaging of the esbuild outputs, which are the one part of the component that no
/// amount of C# gets a chance to notice is missing.
/// </summary>
/// <remarks>
/// <para>Every one of these files is generated at build time and none exist in a clean checkout,
/// so the csproj has to declare items for paths that are not there yet when it is evaluated. Both
/// times that has gone wrong, the build stayed green and the failure surfaced only in a browser:
/// once from embedding the stylesheet under a <c>WasmScripts</c> logical name instead of
/// <c>WasmCSS</c>, and once from an <c>Exists()</c> condition on the item that evaluated before
/// esbuild had run. These tests assert the shipped layout rather than any behaviour, because that
/// is the level the mistake is made at.</para>
/// <para>They are plain unit tests: no browser, no app, so they run in every CI job rather than
/// only the one that can drive Playwright.</para>
/// </remarks>
public sealed class GeneratedAssetPackagingTests
{
    private static Assembly ComponentAssembly => typeof(CodeEditor).Assembly;

    /// <summary>
    /// The stylesheet Uno.Wasm.Bootstrap has to find. Only the <c>.WasmCSS.</c> infix is
    /// load-bearing: the bootstrapper deploys and <c>&lt;link&gt;</c>s embedded resources whose
    /// manifest name contains it, and extracts <c>.WasmScripts.</c> ones as scripts, where a .css
    /// file is dropped without a word. The name is spelled out in full rather than composed from
    /// the assembly name, so that a rename of either half has to be made here too.
    /// </summary>
    private const string WasmStylesheetResourceName =
        "MonacoEditorComponent.WasmCSS.uno-monaco-helpers.css";

    [Fact]
    public void WasmStylesheet_IsEmbeddedUnderTheWasmCssLogicalName()
    {
        Assert.Contains(WasmStylesheetResourceName, ComponentAssembly.GetManifestResourceNames());
    }

    /// <summary>
    /// The resource is the real bundle, not an empty or truncated file. The codicon
    /// <c>@font-face</c> is the marker: it is what every Monaco icon resolves through, and it is
    /// inlined as a data URI by esbuild's <c>.ttf: dataurl</c> loader, so its presence also proves
    /// the font travelled with the stylesheet instead of being left behind as a separate asset
    /// nothing serves.
    /// </summary>
    [Fact]
    public void WasmStylesheet_CarriesTheInlinedCodiconFont()
    {
        using var stream = ComponentAssembly.GetManifestResourceStream(WasmStylesheetResourceName);
        Assert.NotNull(stream);

        using var reader = new StreamReader(stream);
        var css = reader.ReadToEnd();

        // Written as three independent substrings rather than one @font-face pattern: the bundle
        // is minified in every build MSBuild drives, but esbuild.config.mjs also supports an
        // unminified dev build, and the two differ in whitespace and quoting throughout.
        Assert.Contains("@font-face", css);
        Assert.Contains("codicon", css);
        Assert.Contains("url(data:font/ttf;base64,", css);

        // The component's own multi-file header rules ride in the same bundle, from
        // ts-helpermethods/multiDiffEditor.css. Without them the header renders in the document's
        // default serif with its directory undimmed.
        Assert.Contains(".uno-resource-label", css);
    }

    [Theory]
    [InlineData("MonacoEditorComponent.WasmScripts.uno-monaco-helpers.js")]
    [InlineData("MonacoEditorComponent.WasmScripts.workers.editor.worker.js")]
    public void WasmScripts_AreEmbedded(string resourceName)
    {
        Assert.Contains(resourceName, ComponentAssembly.GetManifestResourceNames());
    }

    /// <summary>
    /// Desktop reads these off disk instead: <c>editor.html</c> links the stylesheet and the
    /// script by relative path, and <c>MonacoEnvironment.getWorker</c> resolves the workers out of
    /// the <c>workers/</c> subdirectory. A missing one is a 404 inside the WebView, which surfaces
    /// as an editor that never becomes ready rather than as a build failure.
    /// </summary>
    [Theory]
    [InlineData("editor.html")]
    [InlineData("uno-monaco-helpers.js")]
    [InlineData("uno-monaco-helpers.css")]
    [InlineData("workers/editor.worker.js")]
    [InlineData("workers/json.worker.js")]
    [InlineData("workers/css.worker.js")]
    [InlineData("workers/html.worker.js")]
    [InlineData("workers/ts.worker.js")]
    public void DesktopContent_IsCopiedAlongsideTheComponentAssembly(string relativePath)
    {
        var contentRoot = Path.Combine(AppContext.BaseDirectory, "DesktopContent");
        var path = Path.Combine(contentRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(path), $"Expected desktop content at '{path}'.");
        Assert.True(new FileInfo(path).Length > 0, $"Desktop content at '{path}' is empty.");
    }
}
