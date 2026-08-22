using System.Diagnostics;
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
                SummaryText.Text = "Diff unavailable.";
                return;
            }

            // A side with no lines reports 0 for both of its line numbers -- that is how a
            // pure insertion or deletion is encoded, so it must not be counted as a range.
            var added = changes.Count(c => c.ModifiedEndLineNumber > 0 && c.OriginalEndLineNumber == 0);
            var removed = changes.Count(c => c.OriginalEndLineNumber > 0 && c.ModifiedEndLineNumber == 0);
            var modified = changes.Length - added - removed;

            SummaryText.Text = $"{changes.Length} hunk(s): {added} added, {removed} removed, {modified} modified";

            if (!_diffReadyAnnounced)
            {
                _diffReadyAnnounced = true;
                Console.WriteLine(DiffReadyMarker);
                Debug.WriteLine(DiffReadyMarker);
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
        /// Edits the modified side from C# to confirm the diff recomputes and DiffUpdated fires.
        /// </summary>
        private void Mutate_Click(object sender, RoutedEventArgs e)
            => ModifiedContent += Environment.NewLine + "// appended by the sample";
    }
}
