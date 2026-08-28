using Monaco.Editor;

namespace Monaco
{
    /// <summary>
    /// The direction to move when stepping through diff hunks.
    /// </summary>
    public enum DiffDirection
    {
        /// <summary>Move to the next hunk, wrapping at the end.</summary>
        Next,

        /// <summary>Move to the previous hunk, wrapping at the start.</summary>
        Previous
    }

    partial class DiffCodeEditor
    {
        /// <summary>
        /// Jumps to the next or previous diff hunk.
        /// </summary>
        /// <param name="direction">Which way to step.</param>
        /// <remarks>
        /// Wraps Monaco <c>diffEditor.goToDiff</c>. This replaces the removed
        /// <c>createDiffNavigator</c> API, which no longer exists in Monaco 0.52.
        /// </remarks>
        public async Task GoToDiffAsync(DiffDirection direction)
        {
            // link:otherScriptsToBeOrganized.ts:goToDiff
            await InvokeScriptAsync("goToDiff", direction == DiffDirection.Previous ? "previous" : "next");
        }

        /// <summary>
        /// Scrolls to the first diff hunk, waiting for the diff computation to finish first.
        /// </summary>
        /// <remarks>Wraps Monaco <c>diffEditor.revealFirstDiff</c>.</remarks>
        public async Task RevealFirstDiffAsync()
        {
            // Sent as a raw script rather than through InvokeScriptAsync because that path
            // always appends an argument list after `element`, which would emit a trailing
            // comma for a zero-argument call.
            // link:otherScriptsToBeOrganized.ts:revealFirstDiff
            await SendScriptAsync("revealFirstDiff(element);");
        }

        /// <summary>
        /// Gets the diff hunks Monaco has computed for the current pair of documents.
        /// </summary>
        /// <returns>
        /// The hunks; empty when the two documents are identical, and also empty before the
        /// first computation completes. <see langword="null"/> only when the call could not
        /// reach the editor at all -- it was made before initialization, or the script failed.
        /// </returns>
        /// <remarks>
        /// Wraps Monaco <c>diffEditor.getLineChanges</c>. Monaco itself returns null until the
        /// first computation completes, but that is normalized to an empty array because null
        /// cannot survive the WASM interop path. Distinguish "not computed yet" by whether
        /// <see cref="DiffUpdated"/> has fired, not by the return value, and prefer calling
        /// this from that event rather than immediately after load.
        /// <para>
        /// A hunk reports 0 for both line numbers on whichever side has no lines, which is how
        /// pure insertions and deletions are encoded -- see <see cref="IChange"/>.
        /// </para>
        /// </remarks>
        public async Task<LineChange[]?> GetLineChangesAsync()
        {
            // link:otherScriptsToBeOrganized.ts:getLineChanges
            return await SendScriptAsync<LineChange[]>("getLineChanges(element);");
        }
    }
}
