namespace Monaco
{
    /// <summary>
    /// Identifies which Monaco widget a control bootstraps.
    /// </summary>
    /// <remarks>
    /// This exists because <c>[JSImport]</c> needs a compile-time-constant function name, so
    /// <see cref="WasmCodeEditorPresenter"/> cannot parameterize the bootstrap entry point and
    /// has to select between separate imports. Desktop needs no equivalent -- it goes through
    /// <see cref="EditorHostBase.BootstrapFunctionName"/>.
    /// </remarks>
    internal enum EditorFlavor
    {
        /// <summary>A single-document editor: <see cref="CodeEditor"/>.</summary>
        Code,

        /// <summary>A two-sided comparison of one document: <see cref="DiffCodeEditor"/>.</summary>
        Diff,

        /// <summary>A scrollable list of per-file diffs: <c>MultiDiffCodeEditor</c>.</summary>
        MultiDiff,
    }
}
