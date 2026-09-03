namespace Monaco
{
    partial class MultiDiffCodeEditor
    {
        /// <summary>
        /// Collapses every file's section.
        /// </summary>
        public async Task CollapseAllAsync()
        {
            // link:multiDiffEditor.ts:setAllMultiDiffCollapsed
            await InvokeScriptAsync("setAllMultiDiffCollapsed", true);
        }

        /// <summary>
        /// Expands every file's section.
        /// </summary>
        public async Task ExpandAllAsync()
        {
            // link:multiDiffEditor.ts:setAllMultiDiffCollapsed
            await InvokeScriptAsync("setAllMultiDiffCollapsed", false);
        }

        /// <summary>
        /// Collapses or expands one file's section.
        /// </summary>
        /// <param name="path">The <see cref="DiffFileEntry.Path"/> of the file. Unknown paths are ignored.</param>
        /// <param name="collapsed"><see langword="true"/> to collapse, <see langword="false"/> to expand.</param>
        public async Task SetCollapsedAsync(string path, bool collapsed)
        {
            // link:multiDiffEditor.ts:setMultiDiffCollapsed
            await InvokeScriptAsync("setMultiDiffCollapsed", [path, collapsed]);
        }

        /// <summary>
        /// Scrolls a file into view.
        /// </summary>
        /// <param name="path">The <see cref="DiffFileEntry.Path"/> of the file. Unknown paths are ignored.</param>
        /// <remarks>
        /// The list is virtualized, so a file far down it may not have a live editor until this
        /// brings it into view.
        /// </remarks>
        public async Task RevealFileAsync(string path)
        {
            // link:multiDiffEditor.ts:revealMultiDiffFile
            await InvokeScriptAsync("revealMultiDiffFile", path);
        }
    }
}
