namespace MonacoEditorComponent.Tests;

/// <summary>
/// Page expressions for the re-applied-document tokenization guard.
///
/// <para><b>What the defect is.</b> Neither <c>model.setValue</c> nor
/// <c>monaco.editor.setModelLanguage</c> tokenizes. Both flush the token store and hand the work
/// to Monaco's <c>DefaultBackgroundTokenizer</c>, which schedules itself through
/// <c>requestIdleCallback</c>. A host that repaints every animation frame and owns the main
/// thread -- a focused WebAssembly app -- need never present an idle period, so that callback can
/// stay pending indefinitely and the document paints with the theme's default token class only.
/// The model reports the right language and the right content throughout, so every model-level
/// assertion passes while the page renders as plain text.</para>
///
/// <para><b>Why the visible range has to be held still.</b> Monaco has a second, non-idle
/// tokenization path: an <c>AttachedViewHandler</c> per attached view refreshes the tokens for
/// the lines it can see, on a 50ms timer rather than on idle. It is gated on the visible line
/// range having <i>changed</i> since it last ran, which is what makes this look like a
/// "re-open" bug rather than a tokenization bug -- the first document into a given editor grows
/// the visible range from one line to a viewport and gets tokenized, and every later document of
/// a similar shape leaves that range untouched and does not. Scrolling into unseen lines also
/// repairs itself through that path (with heuristically-guessed start states), which is why only
/// a same-viewport re-apply exposes the starvation.</para>
///
/// <para><b>Why the idle callback is stubbed rather than starved for real.</b> A
/// CDP/Playwright-driven window is unfocused and therefore throttled, so it always has idle time
/// and the background tokenizer always runs; occupying the main thread with timers does not
/// change that, since short bursts still leave gaps the browser uses for idle work. Replacing
/// <c>requestIdleCallback</c> is what makes the starvation deterministic, and it does take effect
/// at runtime: Monaco's idle helper picks its implementation once at bundle load, and the branch
/// it picks when the API exists reads <c>globalThis.requestIdleCallback</c> on every call.</para>
/// </summary>
internal static class TokenizationStarvationCases
{
    /// <summary>
    /// The document pushed into the editor, as an argument rather than inline so it needs no
    /// escaping. C# because the bundled grammar colours its keywords, types and strings
    /// distinctly; the assertion only needs <i>some</i> class other than the default.
    /// </summary>
    public const string Sample = """
        using System;
        using System.Collections.Generic;

        namespace Demo
        {
            /// <summary>A type with enough syntax to colour.</summary>
            public sealed class Program
            {
                private const int Answer = 42;

                // A line comment, which the grammar gives its own token type.
                public static void Main(string[] args)
                {
                    var greeting = "hello";
                    var numbers = new List<int> { 1, 2, 3 };

                    foreach (var number in numbers)
                    {
                        Console.WriteLine($"{greeting} {number} {Answer}");
                    }
                }
            }
        }
        """;

    /// <summary>
    /// The element the component registered the plain editor against, found through the
    /// component's own registry.
    /// </summary>
    /// <remarks>
    /// The element is what every helper takes, and it cannot be derived from the editor: the
    /// host is an ancestor of <c>getDomNode()</c> at no fixed depth. <c>EditorContext._editors</c>
    /// is keyed by it, and esbuild's minifier renames locals but not member names, so the lookup
    /// survives the production bundle.
    /// </remarks>
    private const string HostElementExpressionBody =
        "(() => { const editor = " + DiffEditorCases.StandaloneEditorsExpressionBody + "[0];" +
        " for (const [element, context] of EditorContext._editors) {" +
        " if (context.editor === editor) { return element; } } return null; })()";

    /// <summary>The distinct <c>mtk</c> token classes painted in the plain editor's host.</summary>
    /// <remarks>
    /// Brackets and braces stay coloured even when tokenization never ran, because bracket-pair
    /// colorization is delivered as decorations in co-classes rather than as token classes -- so
    /// this reads the <c>mtk</c> class off each span rather than trusting that anything at all is
    /// coloured.
    /// </remarks>
    public const string PaintedTokenClassesExpression =
        "() => { const host = " + HostElementExpressionBody + "; if (!host) { return ''; }" +
        " const pattern = new RegExp('mtk[0-9]+');" +
        " return [...new Set([...host.querySelectorAll('.view-line span[class*=\"mtk\"]')]" +
        ".map(span => (span.className.match(pattern) || [''])[0]).filter(Boolean))].sort().join(','); }";

    /// <summary>
    /// Applies the document once, normally. This is the load that works: it grows the visible
    /// line range from the reset text's single line to a viewport, so Monaco's own attached-view
    /// refresh tokenizes it. Its purpose is to leave that range equal to what the re-apply below
    /// will produce -- without it the re-apply would change the range too and be repaired by the
    /// same path, and the guard would pass whether the fix is present or not.
    /// </summary>
    public const string PrimeExpression =
        "(sample) => { const host = " + HostElementExpressionBody + "; if (!host) { return false; }" +
        " updateLanguage(host, 'csharp'); updateContent(host, sample); return true; }";

    /// <summary>
    /// Clears the editor the way a consumer clears a preview and re-applies the same document
    /// with the idle callback suspended, then reads back what was painted.
    ///
    /// <para>Returns, in order: the painted token classes, the model's language id, whether the
    /// browser really has the idle API this suspends, and the model's line count. The idle-API
    /// flag is reported rather than assumed: Monaco's idle helper falls back to a
    /// <c>setTimeout</c> deadline when <c>requestIdleCallback</c> is missing, and that fallback
    /// is never starved -- so on a browser without the API this whole guard would pass with or
    /// without the fix, and the test has to say so instead of going quietly green.</para>
    /// </summary>
    /// <remarks>
    /// Stub, act, render and restore all happen inside one evaluation, with the restore in a
    /// <c>finally</c>. The WASM collection shares a single page, so a throw between the stub and
    /// the restore would leave every later test in the collection running against a dead idle
    /// callback -- order-dependent failures with no visible connection to this test. Keeping it
    /// in one synchronous block also removes any await boundary at which a real idle callback
    /// could slip in and tokenize the document behind the assertion's back.
    /// </remarks>
    public static readonly string StarvedReapplyExpression = """
        (sample) => {
            const idleApiPresent = typeof globalThis.requestIdleCallback === 'function'
                && typeof globalThis.cancelIdleCallback === 'function';

            const editor = HOST_EDITORS[0];
            const host = HOST_ELEMENT;
            if (!host) { return ['', '', String(idleApiPresent), '0']; }

            const savedRequestIdleCallback = globalThis.requestIdleCallback;
            globalThis.requestIdleCallback = function () { return 0; };

            try {
                // The clear a consuming app does between documents, then the payload, language
                // first -- the ordering the component's own property pushes produce.
                updateContent(host, '');
                updateLanguage(host, 'plaintext');
                updateLanguage(host, 'csharp');
                updateContent(host, sample);

                // Synchronous, and deliberately not a repair: a forced render paints whatever
                // tokens the model holds, so it cannot mask missing ones.
                editor.render(true);

                const model = editor.getModel();
                const pattern = new RegExp('mtk[0-9]+');
                const painted = [...new Set([...host.querySelectorAll('.view-line span[class*="mtk"]')]
                    .map(span => (span.className.match(pattern) || [''])[0]).filter(Boolean))]
                    .sort().join(',');

                return [painted, model.getLanguageId(), String(idleApiPresent), String(model.getLineCount())];
            } finally {
                globalThis.requestIdleCallback = savedRequestIdleCallback;
            }
        }
        """
        .Replace("HOST_EDITORS", DiffEditorCases.StandaloneEditorsExpressionBody, StringComparison.Ordinal)
        .Replace("HOST_ELEMENT", HostElementExpressionBody, StringComparison.Ordinal);

    /// <summary>
    /// The element a <c>DiffCodeEditor</c> was registered against.
    /// </summary>
    /// <remarks>
    /// Matched on <c>diffEditor</c> rather than on <c>editor</c>: a diff context aliases
    /// <c>editor</c> onto the <i>modified</i> sub-editor, so both a plain editor and a diff
    /// editor answer to that field and the plain one would win on a page hosting both.
    /// </remarks>
    private const string DiffHostElementExpressionBody =
        "(() => { const widget = " + DiffEditorCases.StandaloneDiffEditorsExpressionBody + "[0];" +
        " for (const [element, context] of EditorContext._editors) {" +
        " if (context.diffEditor === widget) { return element; } } return null; })()";

    /// <summary>
    /// The distinct <c>mtk</c> token classes painted in the diff widget's original (left) pane.
    /// </summary>
    /// <remarks>
    /// Scoped to that pane's own DOM node. The two panes are separate editors over separate
    /// models that starve independently, so a query over the whole widget would be satisfied by
    /// whichever side happened to be tokenized.
    /// </remarks>
    public const string OriginalPanePaintedTokenClassesExpression =
        "() => { const widget = " + DiffEditorCases.StandaloneDiffEditorsExpressionBody + "[0];" +
        " const node = widget && widget.getOriginalEditor().getDomNode(); if (!node) { return ''; }" +
        " const pattern = new RegExp('mtk[0-9]+');" +
        " return [...new Set([...node.querySelectorAll('.view-line span[class*=\"mtk\"]')]" +
        ".map(span => (span.className.match(pattern) || [''])[0]).filter(Boolean))].sort().join(','); }";

    /// <summary>
    /// Applies the document to the original side once, normally, for the same reason
    /// <see cref="PrimeExpression"/> does: to settle that pane's visible line range at what the
    /// re-apply will produce, so Monaco's attached-view refresh has no change to react to.
    /// </summary>
    public const string PrimeOriginalExpression =
        "(sample) => { const host = " + DiffHostElementExpressionBody + "; if (!host) { return false; }" +
        " updateOriginalLanguage(host, 'csharp'); updateOriginalContent(host, sample); return true; }";

    /// <summary>
    /// The original-side counterpart of <see cref="StarvedReapplyExpression"/>, returning the
    /// same four values read from the original pane.
    /// </summary>
    public static readonly string StarvedOriginalReapplyExpression = """
        (sample) => {
            const idleApiPresent = typeof globalThis.requestIdleCallback === 'function'
                && typeof globalThis.cancelIdleCallback === 'function';

            const widget = DIFF_EDITORS[0];
            const host = DIFF_HOST_ELEMENT;
            if (!host || !widget) { return ['', '', String(idleApiPresent), '0']; }

            const savedRequestIdleCallback = globalThis.requestIdleCallback;
            globalThis.requestIdleCallback = function () { return 0; };

            try {
                updateOriginalContent(host, '');
                updateOriginalLanguage(host, 'plaintext');
                updateOriginalLanguage(host, 'csharp');
                updateOriginalContent(host, sample);

                const original = widget.getOriginalEditor();
                original.render(true);

                const model = original.getModel();
                const node = original.getDomNode();
                const pattern = new RegExp('mtk[0-9]+');
                const painted = [...new Set([...node.querySelectorAll('.view-line span[class*="mtk"]')]
                    .map(span => (span.className.match(pattern) || [''])[0]).filter(Boolean))]
                    .sort().join(',');

                return [painted, model.getLanguageId(), String(idleApiPresent), String(model.getLineCount())];
            } finally {
                globalThis.requestIdleCallback = savedRequestIdleCallback;
            }
        }
        """
        .Replace("DIFF_EDITORS", DiffEditorCases.StandaloneDiffEditorsExpressionBody, StringComparison.Ordinal)
        .Replace("DIFF_HOST_ELEMENT", DiffHostElementExpressionBody, StringComparison.Ordinal);
}
