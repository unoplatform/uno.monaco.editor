using System.Linq;

using Monaco;

namespace MonacoEditorTestApp
{
    /// <summary>
    /// Sample surface for <see cref="DiffCodeEditor"/>: two-sided content, navigation between
    /// hunks, the side-by-side/inline and whitespace toggles, and a live hunk summary driven
    /// by <see cref="DiffCodeEditor.DiffUpdated"/> plus
    /// <see cref="DiffCodeEditor.GetLineChangesAsync"/>.
    /// </summary>
    public sealed partial class DiffEditorControl : UserControl
    {
        /// <summary>
        /// Emitted once the first diff has been computed, so the CDP test harness can wait for
        /// a settled diff rather than polling. Mirrors EditorControl's TEST_HARNESS_READY.
        /// </summary>
        private const string DiffReadyMarker = "DIFF_HARNESS_READY";

        /// <summary>
        /// Prefix for the per-computation summary marker, formatted
        /// <c>DIFF_HUNKS:{total}:{added}:{removed}</c>. This is the only place the integration
        /// suite can observe the C# side of the feature: the Playwright assertions all read
        /// Monaco's JS API directly, so without this nothing exercises DiffUpdated,
        /// GetLineChangesAsync, or the LineChange deserialization contract end to end.
        /// </summary>
        private const string DiffHunksMarker = "DIFF_HUNKS";

        private bool _diffReadyAnnounced;

        public string OriginalContent
        {
            get => (string)GetValue(OriginalContentProperty);
            set => SetValue(OriginalContentProperty, value);
        }

        public static readonly DependencyProperty OriginalContentProperty =
            DependencyProperty.Register(nameof(OriginalContent), typeof(string), typeof(DiffEditorControl), new PropertyMetadata(""));

        public string ModifiedContent
        {
            get => (string)GetValue(ModifiedContentProperty);
            set => SetValue(ModifiedContentProperty, value);
        }

        public static readonly DependencyProperty ModifiedContentProperty =
            DependencyProperty.Register(nameof(ModifiedContent), typeof(string), typeof(DiffEditorControl), new PropertyMetadata(""));

        public DiffEditorControl()
        {
            InitializeComponent();

            OriginalContent = SampleOriginal;
            ModifiedContent = SampleModified;
        }

        private const string SampleOriginal = """
            using System;

            public class Greeter
            {
                public string Name { get; set; }

                public void Greet()
                {
                    Console.WriteLine("Hello, " + Name);
                }
            }
            """;

        private const string SampleModified = """
            using System;

            public class Greeter
            {
                public required string Name { get; init; }

                public void Greet()
                {
                    Console.WriteLine($"Hello, {Name}!");
                }

                public void Farewell()
                {
                    Console.WriteLine($"Goodbye, {Name}.");
                }
            }
            """;

        private async void Diff_DiffUpdated(DiffCodeEditor sender, EventArgs args)
        {
            var changes = await sender.GetLineChangesAsync();

            if (changes is null)
            {
                // null means the call never reached the editor (uninitialized, or the script
                // failed) rather than "no differences", which is an empty array.
                SummaryText.Text = "Diff unavailable.";
                Console.WriteLine($"{DiffHunksMarker}:unavailable");
                return;
            }

            // A side with no lines reports 0 for both of its line numbers -- that is how a
            // pure insertion or deletion is encoded, so it must not be counted as a range.
            var added = changes.Count(c => c.ModifiedEndLineNumber > 0 && c.OriginalEndLineNumber == 0);
            var removed = changes.Count(c => c.OriginalEndLineNumber > 0 && c.ModifiedEndLineNumber == 0);
            var modified = changes.Length - added - removed;

            SummaryText.Text = $"{changes.Length} hunk(s): {added} added, {removed} removed, {modified} modified";

            // Console.WriteLine, not Debug.WriteLine: the integration suite runs Release
            // builds, where Debug.WriteLine is compiled out.
            Console.WriteLine($"{DiffHunksMarker}:{changes.Length}:{added}:{removed}");

            if (!_diffReadyAnnounced)
            {
                _diffReadyAnnounced = true;
                Console.WriteLine(DiffReadyMarker);
            }
        }

        private async void PreviousDiff_Click(object sender, RoutedEventArgs e)
            => await Diff.GoToDiffAsync(DiffDirection.Previous);

        private async void NextDiff_Click(object sender, RoutedEventArgs e)
            => await Diff.GoToDiffAsync(DiffDirection.Next);

        private async void FirstDiff_Click(object sender, RoutedEventArgs e)
            => await Diff.RevealFirstDiffAsync();

        private void SideBySide_Toggled(object sender, RoutedEventArgs e)
            => Diff.DiffOptions.RenderSideBySide = SideBySideToggle.IsOn;

        private void IgnoreWhitespace_Toggled(object sender, RoutedEventArgs e)
            => Diff.DiffOptions.IgnoreTrimWhitespace = IgnoreWhitespaceToggle.IsOn;

        /// <summary>
        /// Unlocks the original (left) side, which starts read-only as Monaco's default has it.
        /// Written through the options object rather than the <c>OriginalEditable</c> property
        /// deliberately: the two are kept in sync both ways, and this is the direction the
        /// integration suite cannot reach.
        /// </summary>
        private void OriginalEditable_Toggled(object sender, RoutedEventArgs e)
            => Diff.DiffOptions.OriginalEditable = OriginalEditableToggle.IsOn;

        /// <summary>
        /// Locks the modified (right) side, the counterpart to the toggle above: the inherited
        /// ReadOnly governs that side, OriginalEditable the original one.
        /// </summary>
        /// <remarks>
        /// Toggled at runtime, this reaches the modified sub-editor rather than the diff
        /// widget's own option sink, so Monaco keeps drawing the revert arrows and the "Revert
        /// Block" gutter entries. They are inert -- reverting goes through the modified
        /// editor's executeEdits, which a read-only editor refuses -- and that difference is
        /// visible here on purpose: only a ReadOnly already set when the control bootstraps
        /// suppresses the affordance itself. The "Append text" button below still works
        /// either way, because it writes the document from C# rather than through the editor.
        /// </remarks>
        private void ReadOnly_Toggled(object sender, RoutedEventArgs e)
            => Diff.ReadOnly = ReadOnlyToggle.IsOn;

        /// <summary>
        /// Edits the modified side from C# to confirm the diff recomputes and DiffUpdated fires.
        /// </summary>
        private void AppendText_Click(object sender, RoutedEventArgs e)
            => ModifiedContent += Environment.NewLine + "// appended by the sample";
    }
}
