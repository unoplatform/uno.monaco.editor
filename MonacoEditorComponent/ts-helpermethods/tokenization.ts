import * as monaco from 'monaco-editor';

/**
 * Keeping an assigned document tokenized on a host that never goes idle.
 *
 * Neither `model.setValue` nor `monaco.editor.setModelLanguage` tokenizes. Both flush the
 * token store and hand the work to Monaco's `DefaultBackgroundTokenizer`, which schedules
 * itself through `requestIdleCallback`. A host that repaints every animation frame and owns
 * the main thread -- a focused WebAssembly app -- need never present an idle period, so that
 * callback can stay pending indefinitely: the document keeps reporting the right language and
 * the right content while every span paints with the theme's default token class.
 *
 * Monaco does have a second, non-idle path -- an `AttachedViewHandler` per attached view
 * refreshes the tokens for the lines it can see on a 50ms timer -- but it is gated on the
 * visible line range having *changed* since it last ran. That is what makes this look like a
 * "re-open" bug rather than a tokenization bug: the first document into a given editor grows
 * the visible range from one line to a viewport and gets tokenized, while every later document
 * of a similar shape leaves that range untouched and does not. Scrolling into unseen lines
 * goes through the same path and repairs itself, so this only has to cover the assignment.
 *
 * `monaco.editor.create()` already tokenizes what it can see, which is the other half of why
 * first loads look fine. Doing the same after an assignment is what this module is for.
 */

/**
 * Lines tokenized past the last visible one, so a small scroll or a line-height change does
 * not immediately expose untokenized text.
 */
const LookAheadLines = 20;

/**
 * Floor applied when the editor reports no visible range -- a host that has not been laid out
 * yet -- and headroom for a container that grows after the document was assigned. A height
 * change alone does *not* trigger Monaco's attached-view refresh, so a host that arrives at
 * 300px and expands to fill a window would otherwise show untokenized lines in the space it
 * gained. Measured against this bundle's defaults a viewport line is ~19px, so 100 lines is
 * headroom for roughly a full-screen editor -- proportionally less if a consumer raises
 * fontSize or lineHeight, which is why this is headroom and not a guarantee.
 */
const MinimumLines = 100;

/**
 * Ceiling on one synchronous pass. `forceTokenization` runs on the single WebAssembly UI
 * thread, and a collapsed or unlaid-out host can report a visible range spanning hundreds of
 * lines, so the viewport is not a bound that can be trusted on its own. Lines past this stay
 * with the background tokenizer, and with the attached-view refresh that scrolling drives.
 */
const MaximumLines = 2000;

/**
 * The model's tokenization part. Absent from `monaco.d.ts` -- it is public at runtime but not
 * in the published typings -- so it is described here rather than cast blindly, and every call
 * site below checks for it instead of assuming a given Monaco version exposes it.
 */
interface TokenizationPart {
    forceTokenization(lineNumber: number): void;
}

/**
 * Tokenize the lines `editor` can currently see, synchronously.
 *
 * Call after assigning content or a language to a model that is already attached to an editor.
 * Cheap when the lines are already tokenized: `forceTokenization` stops at the first valid
 * line, and a pass that computes nothing fires no token-change event.
 *
 * @param model The model that was just assigned to. Ignored when absent or disposed.
 * @param editor The editor showing it, used only for its visible range. When it is absent, has
 * no layout yet, or reports nothing, {@link MinimumLines} is tokenized instead.
 */
export const tokenizeVisibleRange = function (
    model: monaco.editor.ITextModel | undefined,
    editor: monaco.editor.ICodeEditor | undefined
): void {
    if (!model || model.isDisposed()) {
        return;
    }

    const tokenization = (model as any).tokenization as TokenizationPart | undefined;
    if (!tokenization || typeof tokenization.forceTokenization !== 'function') {
        return;
    }

    let through = MinimumLines;

    try {
        const ranges = editor ? editor.getVisibleRanges() : [];
        if (ranges.length) {
            through = Math.max(through, ranges[ranges.length - 1].endLineNumber + LookAheadLines);
        }
    } catch {
        // A widget mid-teardown, or one that has never been laid out, throws here rather than
        // reporting an empty range. The floor is the right answer in that case too.
    }

    through = Math.max(1, Math.min(through, MaximumLines, model.getLineCount()));

    try {
        tokenization.forceTokenization(through);
    } catch (error) {
        // Highlighting is not worth failing a content or language push over.
        console.warn('[tokenizeVisibleRange] forceTokenization failed:', error);
    }
};
