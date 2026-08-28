using System;
using System.Linq;

using Monaco;

namespace MonacoEditorTestApp
{
    /// <summary>
    /// Sample surface for <see cref="MultiDiffCodeEditor"/>: a file list covering all four states
    /// a multi-file diff can show -- modified, added, deleted, renamed -- plus collapse, reveal,
    /// and live mutation of the collection and of entries already in it.
    /// </summary>
    public sealed partial class MultiDiffEditorControl : UserControl
    {
        /// <summary>
        /// Emitted once the first diff has been computed, so the CDP test harness can wait for a
        /// settled view rather than polling. Mirrors DiffEditorControl's DIFF_HARNESS_READY.
        /// </summary>
        private const string MultiDiffReadyMarker = "MULTIDIFF_HARNESS_READY";

        /// <summary>
        /// Prefix for the per-computation summary marker, formatted
        /// <c>MULTIDIFF_FILES:{count}:{added}:{deleted}:{renamed}</c>. This is the only place the
        /// integration suite can observe the C# side of the feature -- the Playwright assertions
        /// all read the DOM and Monaco's JS API directly, so without this nothing exercises
        /// DiffUpdated, the Files collection, or the DiffFileEntry serialization contract end to
        /// end.
        /// </summary>
        private const string MultiDiffFilesMarker = "MULTIDIFF_FILES";

        private bool _readyAnnounced;
        private int _addedFileCount;

        public MultiDiffEditorControl()
        {
            InitializeComponent();

            // All four states, so the sample exercises every badge the widget can render and the
            // null-versus-empty-string distinction that drives them.
            MultiDiff.Files.Add(new DiffFileEntry
            {
                Path = "src/Calculator.cs",
                OriginalText = """
                    public int Add(int a, int b)
                    {
                        return a + b;
                    }
                    """,
                ModifiedText = """
                    public int Add(int a, int b)
                    {
                        checked { return a + b; }
                    }
                    """,
            });

            // OriginalText = null (not "") is what marks the file as added.
            MultiDiff.Files.Add(new DiffFileEntry
            {
                Path = "src/OverflowPolicy.cs",
                OriginalText = null,
                ModifiedText = """
                    public enum OverflowPolicy
                    {
                        Checked,
                        Unchecked,
                    }
                    """,
            });

            // ModifiedText = null is what marks the file as deleted.
            MultiDiff.Files.Add(new DiffFileEntry
            {
                Path = "src/LegacyMath.cs",
                OriginalText = """
                    public static class LegacyMath
                    {
                        public static int Add(int a, int b) => a + b;
                    }
                    """,
                ModifiedText = null,
            });

            // A differing OriginalPath is what marks the file as renamed.
            MultiDiff.Files.Add(new DiffFileEntry
            {
                Path = "docs/arithmetic.md",
                OriginalPath = "docs/math.md",
                OriginalText = "# Math\n\nAddition.\n",
                ModifiedText = "# Arithmetic\n\nAddition, with overflow checking.\n",
                Language = "markdown",
            });
        }

        private async void MultiDiff_DiffUpdated(MultiDiffCodeEditor sender, EventArgs args)
        {
            var files = MultiDiff.Files;
            var added = files.Count(f => f.OriginalText is null);
            var deleted = files.Count(f => f.ModifiedText is null);
            var renamed = files.Count(f => !string.IsNullOrEmpty(f.OriginalPath) && f.OriginalPath != f.Path);

            SummaryText.Text = $"{files.Count} file(s): {added} added, {deleted} deleted, {renamed} renamed.";

            // Console.WriteLine, not Debug.WriteLine: the integration suite runs Release.
            Console.WriteLine($"{MultiDiffFilesMarker}:{files.Count}:{added}:{deleted}:{renamed}");

            if (!_readyAnnounced)
            {
                _readyAnnounced = true;
                Console.WriteLine(MultiDiffReadyMarker);
            }

            await Task.CompletedTask;
        }

        private async void CollapseAll_Click(object sender, RoutedEventArgs e)
            => await MultiDiff.CollapseAllAsync();

        private async void ExpandAll_Click(object sender, RoutedEventArgs e)
            => await MultiDiff.ExpandAllAsync();

        private async void RevealLast_Click(object sender, RoutedEventArgs e)
        {
            if (MultiDiff.Files.Count > 0)
            {
                await MultiDiff.RevealFileAsync(MultiDiff.Files[^1].Path);
            }
        }

        /// <summary>
        /// Mutates an entry already in the collection, rather than the collection itself -- the
        /// path that needs per-item PropertyChanged tracking to reach Monaco at all.
        /// </summary>
        private void AppendText_Click(object sender, RoutedEventArgs e)
        {
            if (MultiDiff.Files.Count > 0)
            {
                MultiDiff.Files[0].ModifiedText += "\n// appended\n";
            }
        }

        private void AddFile_Click(object sender, RoutedEventArgs e)
        {
            _addedFileCount++;
            MultiDiff.Files.Add(new DiffFileEntry
            {
                Path = $"src/Generated{_addedFileCount}.cs",
                OriginalText = $"// generated {_addedFileCount}\n",
                ModifiedText = $"// generated {_addedFileCount}\npublic sealed class Generated{_addedFileCount} {{ }}\n",
            });
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (MultiDiff.Files.Count > 0)
            {
                MultiDiff.Files.RemoveAt(MultiDiff.Files.Count - 1);
            }
        }

        private void SideBySide_Toggled(object sender, RoutedEventArgs e)
        {
            if (MultiDiff?.DiffOptions is { } options)
            {
                options.RenderSideBySide = SideBySideToggle.IsOn;
            }
        }
    }
}
