namespace MonacoEditorComponent.Tests;

/// <summary>
/// Shared page expressions and expectations for the <c>DiffCodeEditor</c> control, consumed
/// by both the WASM and desktop integration tests so the two cannot drift apart.
/// <para>
/// What these guard is the aliasing the control is built on: <c>EditorContext.editor</c> holds
/// the <i>modified sub-editor</i> of the diff widget, which is what lets every pre-existing
/// helper keep working unchanged. If that aliasing regresses, the two documents stop being
/// independently addressable and <c>getModifiedEditor()</c> stops agreeing with the value the
/// content listener reports.
/// </para>
/// </summary>
internal static class DiffEditorCases
{
    /// <summary>
    /// The code editors on the page that are <i>not</i> part of a diff widget, as a JS array.
    /// </summary>
    /// <remarks>
    /// <c>monaco.editor.getEditors()</c> lists every code editor the service knows about, and a
    /// diff widget's original and modified panes are themselves standalone code editors -- so a
    /// page showing one plain editor and one diff editor reports three. Tests that mean "the
    /// plain editor" have to subtract the sub-editors rather than index into getEditors().
    /// </remarks>
    public const string StandaloneEditorsExpressionBody =
        "(() => { const subs = new Set(monaco.editor.getDiffEditors()" +
        ".flatMap(d => [d.getOriginalEditor(), d.getModifiedEditor()]));" +
        " return monaco.editor.getEditors().filter(e => !subs.has(e)); })()";

    /// <summary>Number of editors on the page that are not part of a diff widget.</summary>
    public const string StandaloneEditorCountExpression =
        "() => " + StandaloneEditorsExpressionBody + ".length";

    /// <summary>Whether the first non-diff editor has a model.</summary>
    public const string StandaloneEditorHasModelExpression =
        "() => " + StandaloneEditorsExpressionBody + "[0].getModel() !== null";

    /// <summary>Whether the page hosts a Monaco diff editor at all.</summary>
    public const string IsDiffEditorPresentExpression =
        "() => typeof monaco !== 'undefined' && monaco.editor.getDiffEditors().length > 0";

    /// <summary>The original (left) document's text.</summary>
    public const string OriginalValueExpression =
        "() => monaco.editor.getDiffEditors()[0].getOriginalEditor().getValue()";

    /// <summary>The modified (right) document's text.</summary>
    public const string ModifiedValueExpression =
        "() => monaco.editor.getDiffEditors()[0].getModifiedEditor().getValue()";

    /// <summary>The original document's language id.</summary>
    public const string OriginalLanguageExpression =
        "() => monaco.editor.getDiffEditors()[0].getOriginalEditor().getModel().getLanguageId()";

    /// <summary>The modified document's language id.</summary>
    public const string ModifiedLanguageExpression =
        "() => monaco.editor.getDiffEditors()[0].getModifiedEditor().getModel().getLanguageId()";

    /// <summary>
    /// The two models must be distinct instances. A regression that pointed both sides at one
    /// model would still render and still diff (against itself, producing zero hunks), so this
    /// is checked explicitly rather than inferred from the hunk count.
    /// </summary>
    public const string ModelsAreDistinctExpression =
        "() => { const d = monaco.editor.getDiffEditors()[0];" +
        " return d.getOriginalEditor().getModel() !== d.getModifiedEditor().getModel(); }";

    /// <summary>
    /// Number of computed hunks, or -1 while Monaco is still computing. The helper normalizes
    /// Monaco's null to an empty array, so this reads the widget directly to keep the
    /// distinction the C# API deliberately drops.
    /// </summary>
    public const string LineChangeCountExpression =
        "() => { const c = monaco.editor.getDiffEditors()[0].getLineChanges(); return c === null ? -1 : c.length; }";

    /// <summary>
    /// Whether the diff widget's root element carries Monaco's <c>.monaco-diff-editor</c>
    /// class. Doubles as a check that the diff stylesheet reached the page: the split view,
    /// overview ruler, and change decorations are all styled off that class, and it is the
    /// one part of the payload a plain editor never exercises.
    /// </summary>
    public const string DiffEditorRootExpression =
        "() => document.querySelectorAll('.monaco-diff-editor').length > 0";

    /// <summary>
    /// Waits until Monaco has computed a diff with at least one hunk. Diff computation is
    /// asynchronous (and on WASM runs on the main thread via Monaco's worker fallback), so
    /// tests must wait rather than read immediately after load.
    /// </summary>
    public const string HasComputedDiffExpression =
        "() => { const editors = monaco.editor.getDiffEditors();" +
        " if (!editors.length) return false;" +
        " const c = editors[0].getLineChanges(); return c !== null && c.length > 0; }";

    /// <summary>
    /// Whether the diff has been computed and found no differences. Distinct from
    /// "not computed yet", which reports null rather than an empty array.
    /// </summary>
    public const string NoRemainingHunksExpression =
        "() => { const c = monaco.editor.getDiffEditors()[0].getLineChanges(); return c !== null && c.length === 0; }";

    /// <summary>
    /// Replaces the modified document's text, so a test can drive a recomputation the same way
    /// a user typing would. Returns nothing; pair with <see cref="HasComputedDiffExpression"/>.
    /// </summary>
    public const string SetModifiedValueExpression =
        "(value) => monaco.editor.getDiffEditors()[0].getModifiedEditor().getModel().setValue(value)";

    /// <summary>
    /// Whether the original (left) sub-editor is read-only. Monaco derives this from the diff
    /// widget's <c>originalEditable</c> option and overwrites it on every option change, so it
    /// is the effective readout of <c>DiffCodeEditor.OriginalEditable</c> -- and the only diff
    /// option whose effect is observable on a sub-editor rather than on the widget.
    /// </summary>
    /// <remarks>
    /// Reads the option id from <c>monaco.editor.EditorOption</c> rather than hardcoding its
    /// numeric value, which shifts between Monaco releases.
    /// </remarks>
    public const string OriginalEditorReadOnlyExpression =
        "() => monaco.editor.getDiffEditors()[0].getOriginalEditor().getOption(monaco.editor.EditorOption.readOnly)";

    /// <summary>
    /// Waits until the original side is present and locked. Phrased as a wait rather than a
    /// read because WASM delivers the diff options after construction, so a diff editor can
    /// exist for a moment before they reach it.
    /// </summary>
    public const string OriginalEditorLockedExpression =
        "() => { const editors = monaco.editor.getDiffEditors(); if (!editors.length) return false;" +
        " return editors[0].getOriginalEditor().getOption(monaco.editor.EditorOption.readOnly) === true; }";

    /// <summary>
    /// Whether the modified (right) sub-editor is read-only. Asserted alongside
    /// <see cref="OriginalEditorReadOnlyExpression"/>: the two sides lock independently, so
    /// the original's lock must not have leaked across to the modified one.
    /// </summary>
    public const string ModifiedEditorReadOnlyExpression =
        "() => monaco.editor.getDiffEditors()[0].getModifiedEditor().getOption(monaco.editor.EditorOption.readOnly)";

    /// <summary>
    /// Whether Monaco's own stylesheet reached the page, rather than only the theme rules
    /// Monaco injects at runtime. Three independent signals, because the runtime-injected
    /// rules also match <c>.monaco-editor</c> and would satisfy a looser check on their own:
    /// the exact <c>.monaco-editor</c> selector, which only the bundled sheet declares; the
    /// <c>position: relative</c> it sets, which nothing else supplies; and the codicon font
    /// family from its <c>@font-face</c>, whose absence turns every Monaco icon into tofu.
    /// </summary>
    /// <remarks>
    /// This is a delivery check, not a styling check. On WASM the stylesheet only reaches the
    /// document when it is embedded under a <c>WasmCSS</c> logical name: Uno.Wasm.Bootstrap
    /// extracts <c>WasmScripts</c> resources as scripts and drops a <c>.css</c> file among them
    /// without a diagnostic, which is how the sheet went missing while every other WASM test
    /// still passed.
    /// </remarks>
    public const string MonacoStylesheetAppliedExpression =
        "() => { const rules = [];" +
        " for (const sheet of document.styleSheets) { try { rules.push(...sheet.cssRules); } catch (e) { /* cross-origin */ } }" +
        " if (!rules.some(r => (r.selectorText || '').trim() === '.monaco-editor')) return false;" +
        " const editor = document.querySelector('.monaco-editor');" +
        " if (!editor || getComputedStyle(editor).position !== 'relative') return false;" +
        " return [...document.fonts].some(f => f.family === 'codicon'); }";

    /// <summary>
    /// The sample the test app loads. Kept in sync with <c>DiffEditorControl</c> only loosely:
    /// assertions below check structural facts (the sides differ, hunks exist) rather than
    /// exact text, so tweaking the sample does not break the tests.
    /// </summary>
    public const string SharedFirstLine = "using System;";
}
