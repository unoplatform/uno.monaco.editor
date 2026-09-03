using Microsoft.UI.Xaml;

using Monaco.Editor;

using Windows.Foundation.Collections;

namespace Monaco;

partial class MultiDiffCodeEditor
{
    /* Files: The per-file documents to render, in order. Mutating the collection or any entry
     *      re-pushes the whole list; the JS side reconciles it incrementally.
     * DiffOptions: Diff options applied to every file. Read-only is enforced on top of it.
     * ActiveFilePath: Read-only. The path of the file that currently holds focus.
     */

    #region DependencyProperty: Files

    /// <summary>Identifies the <see cref="Files"/> dependency property.</summary>
    public static DependencyProperty FilesProperty { get; } = DependencyProperty.Register(
        nameof(Files),
        typeof(IObservableVector<DiffFileEntry>),
        typeof(MultiDiffCodeEditor),
        new PropertyMetadata(default(IObservableVector<DiffFileEntry>), OnFilesChanged));

    /// <summary>
    /// Gets or sets the files to compare, in display order.
    /// </summary>
    /// <remarks>
    /// Adding, removing or reordering entries re-pushes the list, and so does changing a property
    /// on an entry already in it. Reconciliation on the JavaScript side is by
    /// <see cref="DiffFileEntry.Path"/>, so a file that keeps its path keeps its scroll offset and
    /// collapsed state across the push.
    /// <para>
    /// Paths must be unique; a duplicate is skipped rather than rendered twice.
    /// </para>
    /// </remarks>
    public IObservableVector<DiffFileEntry> Files
    {
        get => (IObservableVector<DiffFileEntry>)GetValue(FilesProperty);
        set => SetValue(FilesProperty, value);
    }

    #endregion
    #region DependencyProperty: DiffOptions

    /// <summary>Identifies the <see cref="DiffOptions"/> dependency property.</summary>
    public static DependencyProperty DiffOptionsProperty { get; } = DependencyProperty.Register(
        nameof(DiffOptions),
        typeof(DiffEditorOptions),
        typeof(MultiDiffCodeEditor),
        new PropertyMetadata(default(DiffEditorOptions), OnDiffOptionsChanged));

    /// <summary>
    /// Gets or sets the diff options applied to every file.
    /// </summary>
    /// <remarks>
    /// <see cref="DiffEditorOptions.HideUnchangedRegions"/> is forced on by Monaco for every file
    /// in a multi-file view and cannot be turned off here, and this control is read-only, so
    /// <see cref="DiffEditorOptions.OriginalEditable"/> is ignored. Everything else -- side-by-side
    /// versus inline, whitespace handling, the diff algorithm -- applies as usual.
    /// </remarks>
    public DiffEditorOptions DiffOptions
    {
        get => (DiffEditorOptions)GetValue(DiffOptionsProperty);
        set => SetValue(DiffOptionsProperty, value);
    }

    #endregion
    #region DependencyProperty: ActiveFilePath

    /// <summary>Identifies the <see cref="ActiveFilePath"/> dependency property.</summary>
    public static DependencyProperty ActiveFilePathProperty { get; } = DependencyProperty.Register(
        nameof(ActiveFilePath),
        typeof(string),
        typeof(MultiDiffCodeEditor),
        new PropertyMetadata(default(string)));

    /// <summary>
    /// Gets the <see cref="DiffFileEntry.Path"/> of the file that currently holds focus, or
    /// <see langword="null"/> when none does.
    /// </summary>
    public string? ActiveFilePath
    {
        get => (string?)GetValue(ActiveFilePathProperty);
        private set => SetValue(ActiveFilePathProperty, value);
    }

    #endregion

    private static void OnFilesChanged(DependencyObject control, DependencyPropertyChangedEventArgs e) => ((MultiDiffCodeEditor)control).OnFilesChanged(e);
    private static void OnDiffOptionsChanged(DependencyObject control, DependencyPropertyChangedEventArgs e) => ((MultiDiffCodeEditor)control).OnDiffOptionsChanged(e);
}
