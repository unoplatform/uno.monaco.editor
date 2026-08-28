namespace MonacoEditorComponent.Tests;

/// <summary>
/// Shared page expressions and expectations for the <c>MultiDiffCodeEditor</c> control, consumed
/// by both the WASM and desktop integration tests so the two cannot drift apart.
/// <para>
/// Two properties of the widget shape everything here. It is <b>virtualized</b>: only the files
/// near the viewport have live editors, and the pooled <c>.multiDiffEntry</c> templates are
/// hidden rather than removed when they fall out of use -- so neither the number of DOM entries
/// nor the number of registered diff editors is the number of files. And it <b>owns its models</b>:
/// the JS layer creates and disposes them, which makes <c>monaco.editor.getModels()</c> the
/// honest readout of what a push actually did.
/// </para>
/// </summary>
internal static class MultiDiffEditorCases
{
    /// <summary>The multi-file diff widget's root element, as a JS expression.</summary>
    public const string RootExpressionBody =
        "document.querySelector('.monaco-component.multiDiffEditor')";

    /// <summary>
    /// The host element the control's JS state is keyed by, as a JS expression.
    /// </summary>
    /// <remarks>
    /// Derived from the widget's own root rather than looked up by id, because the two targets
    /// name that element differently: desktop's editor.html declares <c>editor-container</c>,
    /// while on WASM it is a generated Uno element id. <c>MultiDiffEditorWidgetImpl</c> does
    /// <c>element.replaceChildren(root)</c>, so the host is always the root's parent.
    /// </remarks>
    public const string HostExpressionBody = RootExpressionBody + "?.parentElement";

    /// <summary>Whether the page hosts a multi-file diff widget at all.</summary>
    public const string IsPresentExpression =
        "() => " + RootExpressionBody + " !== null";

    /// <summary>
    /// The file sections that are actually on screen, as a JS array.
    /// </summary>
    /// <remarks>
    /// The widget recycles its item templates through an ObjectPool and hides the spares with
    /// <c>visibility: hidden</c> instead of removing them, so a bare
    /// <c>querySelectorAll('.multiDiffEntry')</c> keeps counting files that are no longer in the
    /// list. Filtering on visibility is what makes the count mean "files currently rendered".
    /// </remarks>
    public const string VisibleEntriesExpressionBody =
        "[...document.querySelectorAll('.multiDiffEntry')]" +
        ".filter(e => getComputedStyle(e).visibility !== 'hidden')";

    /// <summary>Number of file sections currently rendered.</summary>
    public const string VisibleEntryCountExpression =
        "() => " + VisibleEntriesExpressionBody + ".length";

    /// <summary>
    /// Whether the widget has rendered at least one file section. The general readiness signal:
    /// the list is virtualized, so how many sections are on screen depends on the viewport and
    /// on whether they are collapsed, but "none at all" means nothing has been pushed yet.
    /// </summary>
    public const string AnyEntryRenderedExpression =
        "() => " + VisibleEntriesExpressionBody + ".length > 0";

    /// <summary>
    /// The primary (modified-side) header label of each rendered file, in display order.
    /// Populated by the control's own <c>createResourceLabel</c>; empty labels mean the
    /// <c>IWorkbenchUIElementFactory</c> never reached the item template.
    /// </summary>
    public const string HeaderLabelsExpression =
        "() => " + VisibleEntriesExpressionBody +
        ".map(e => e.querySelector('.title.modified')?.textContent ?? '')";

    /// <summary>
    /// The status badge of each rendered file, in display order: <c>A</c> added, <c>D</c> deleted,
    /// <c>R</c> renamed, empty for a plain modification.
    /// </summary>
    /// <remarks>
    /// A badge on a plainly-modified file means the original and modified model URIs disagree on
    /// their path -- the failure mode of putting the side discriminator anywhere but the URI
    /// authority, which makes Monaco read every file as a rename.
    /// </remarks>
    public const string StatusBadgesExpression =
        "() => " + VisibleEntriesExpressionBody +
        ".map(e => (e.querySelector('.status')?.textContent ?? '').trim())";

    /// <summary>
    /// The number of computed hunks for each per-file diff editor inside the widget, or -1 for
    /// one that has not finished computing.
    /// </summary>
    public const string PerFileHunkCountsExpression =
        "() => monaco.editor.getDiffEditors()" +
        ".filter(d => d.getContainerDomNode().closest('.multiDiffEditor'))" +
        ".map(d => { const c = d.getLineChanges(); return c === null ? -1 : c.length; })";

    /// <summary>
    /// Waits until every per-file editor inside the widget has finished computing its diff.
    /// Diff computation is asynchronous and runs on the shared editor worker, so with N files
    /// there are N computations to settle rather than one.
    /// </summary>
    /// <remarks>
    /// Only satisfiable while files are expanded. Collapsing a file lets the widget's ObjectPool
    /// reclaim its template, which calls <c>setDiffModel(null)</c> and makes
    /// <c>getLineChanges()</c> report null again -- so this is a wait for a settled *expanded*
    /// view, not a general readiness check. Use <see cref="AnyEntryRenderedExpression"/> for that.
    /// </remarks>
    public const string AllDiffsComputedExpression =
        "() => { const eds = monaco.editor.getDiffEditors()" +
        ".filter(d => d.getContainerDomNode().closest('.multiDiffEditor'));" +
        " return eds.length > 0 && eds.every(d => d.getLineChanges() !== null); }";

    /// <summary>
    /// Whether every per-file editor is read-only on both sides. The control is a read-only
    /// viewer, and the two sides lock through different options, so both are checked.
    /// </summary>
    public const string AllFilesReadOnlyExpression =
        "() => { const eds = monaco.editor.getDiffEditors()" +
        ".filter(d => d.getContainerDomNode().closest('.multiDiffEditor'));" +
        " return eds.length > 0 && eds.every(d =>" +
        " d.getModifiedEditor().getOption(monaco.editor.EditorOption.readOnly) === true" +
        " && d.getOriginalEditor().getOption(monaco.editor.EditorOption.readOnly) === true); }";

    /// <summary>Whether every rendered file's section is collapsed.</summary>
    public const string AllCollapsedExpression =
        "() => { const entries = " + VisibleEntriesExpressionBody + ";" +
        " return entries.length > 0 && entries.every(e =>" +
        " e.querySelector('.collapse-button .codicon-chevron-right') !== null); }";

    /// <summary>Whether every rendered file's section is expanded.</summary>
    public const string NoneCollapsedExpression =
        "() => { const entries = " + VisibleEntriesExpressionBody + ";" +
        " return entries.length > 0 && entries.every(e =>" +
        " e.querySelector('.collapse-button .codicon-chevron-down') !== null); }";

    /// <summary>
    /// Whether the collapse chevron resolves to a real codicon glyph rather than nothing.
    /// </summary>
    /// <remarks>
    /// The per-icon <c>content</c> rules are generated at runtime by the standalone theme
    /// service and only injected once an editor container is registered -- which nothing on the
    /// multi-file path does for us. When that registration is missing the whole widget renders
    /// unstyled, and this is the cheapest observable symptom.
    /// </remarks>
    public const string ChevronGlyphRenderedExpression =
        "() => { const icon = document.querySelector('.multiDiffEntry .collapse-button .codicon');" +
        " if (!icon) return false;" +
        " const content = getComputedStyle(icon, '::before').content;" +
        " return content !== 'none' && content !== 'normal' && content.length > 2; }";

    /// <summary>
    /// Whether the Monaco theme stylesheet -- the one carrying the <c>--vscode-*</c> variables
    /// and the runtime codicon rules -- is on the page.
    /// </summary>
    public const string ThemeStylesheetPresentExpression =
        "() => document.querySelectorAll('style.monaco-colors').length > 0";

    /// <summary>Total number of text models alive, the readout for model-leak assertions.</summary>
    public const string ModelCountExpression =
        "() => monaco.editor.getModels().length";

    /// <summary>
    /// Whether a file with the given path is currently rendered. Takes the path as its argument.
    /// </summary>
    public const string IsPathRenderedExpression =
        "(path) => " + VisibleEntriesExpressionBody +
        ".some(e => (e.querySelector('.title.modified')?.textContent ?? '') === path)";

    /// <summary>
    /// The status badge of the file with the given path, or <c>null</c> when it is not rendered.
    /// Takes the path as its argument.
    /// </summary>
    public const string BadgeForPathExpression =
        "(path) => { const e = " + VisibleEntriesExpressionBody +
        ".find(x => (x.querySelector('.title.modified')?.textContent ?? '') === path);" +
        " return e === undefined ? null : (e.querySelector('.status')?.textContent ?? '').trim(); }";

    /// <summary>Scrolls the file with the given path into view. Takes the path as its argument.</summary>
    public const string RevealPathExpression =
        "(path) => globalThis.revealMultiDiffFile(" + HostExpressionBody + ", path)";

    /// <summary>Collapses or expands every file. Takes the collapsed flag as its argument.</summary>
    public const string SetAllCollapsedExpression =
        "(collapsed) => globalThis.setAllMultiDiffCollapsed(" + HostExpressionBody + ", collapsed)";

    /// <summary>Whether the widget's "No Changed Files" placeholder is showing.</summary>
    public const string PlaceholderVisibleExpression =
        "() => document.querySelector('.multiDiffEditor .placeholder.visible') !== null";

    /// <summary>
    /// Marker the sample writes once the first diff has settled. Console.WriteLine, not
    /// Debug.WriteLine, because the integration suite runs Release.
    /// </summary>
    public const string ReadyMarker = "MULTIDIFF_HARNESS_READY";

    /// <summary>
    /// Prefix of the sample's per-computation summary, <c>MULTIDIFF_FILES:{count}:{added}:{deleted}:{renamed}</c>.
    /// The only place the suite observes the managed side: DiffUpdated firing, the Files
    /// collection, and the DiffFileEntry serialization contract.
    /// </summary>
    public const string FilesMarkerPrefix = "MULTIDIFF_FILES:";

    /// <summary>Files the sample seeds, in display order.</summary>
    public static readonly string[] SampleFilePaths =
    [
        "src/Calculator.cs",
        "src/OverflowPolicy.cs",
        "src/LegacyMath.cs",
        "docs/arithmetic.md",
    ];

    /// <summary>Status badges the sample's four files must render.</summary>
    public static readonly string[] SampleStatusBadges = ["", "A", "D", "R"];

    /// <summary>
    /// A minimal file list as a JS literal, for tests that drive <c>updateMultiDiffFiles</c>
    /// directly rather than through the managed collection. Deliberately not the sample's list:
    /// pushing over it and back would race the control's own pushes.
    /// </summary>
    public const string ProbeFilesLiteral =
        "[{path:'probe/a.cs',originalText:'class A { }',modifiedText:'class A { int x; }'}," +
        "{path:'probe/b.cs',originalText:'class B { }',modifiedText:'class B { int y; }'}]";

    /// <summary>Pushes <see cref="ProbeFilesLiteral"/>, optionally minus one path.</summary>
    public static string PushProbeFilesExpression(string? excludedPath = null)
    {
        var list = excludedPath is null
            ? ProbeFilesLiteral
            : $"{ProbeFilesLiteral}.filter(f => f.path !== '{excludedPath}')";

        return "() => globalThis.updateMultiDiffFiles(" + HostExpressionBody + ", " + list + ")";
    }

    /// <summary>Restores the widget to an empty list, releasing every probe model.</summary>
    public const string ClearFilesExpression =
        "() => globalThis.updateMultiDiffFiles(" + HostExpressionBody + ", [])";

    /// <summary>
    /// The sample's four files as a JS literal, so a test that pushed its own list can put an
    /// equivalent one back -- the app is shared across the collection and C# will not re-push on
    /// its own.
    /// </summary>
    /// <remarks>
    /// Structurally, not textually, in sync with <c>MultiDiffEditorControl</c>: same paths, same
    /// four states, each side differing enough to produce at least one hunk. Assertions here
    /// check structure rather than exact text, so the sample can be edited without breaking them.
    /// </remarks>
    public const string SampleFilesLiteral =
        "[{path:'src/Calculator.cs',originalText:'int Add(int a, int b) => a + b;'," +
        "modifiedText:'int Add(int a, int b) => checked(a + b);'}," +
        "{path:'src/OverflowPolicy.cs',originalText:null,modifiedText:'enum OverflowPolicy { Checked }'}," +
        "{path:'src/LegacyMath.cs',originalText:'static class LegacyMath { }',modifiedText:null}," +
        "{path:'docs/arithmetic.md',originalPath:'docs/math.md',originalText:'# Math'," +
        "modifiedText:'# Arithmetic',language:'markdown'}]";

    /// <summary>Pushes <see cref="SampleFilesLiteral"/> back onto the widget.</summary>
    public const string RestoreSampleFilesExpression =
        "() => globalThis.updateMultiDiffFiles(" + HostExpressionBody + ", " + SampleFilesLiteral + ")";
}
