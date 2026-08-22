#nullable enable
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Controls collapsing of regions that contain no changes.
    /// </summary>
    /// <remarks>
    /// This is a plain value object: mutating it in place does not raise
    /// <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> on the owning
    /// <see cref="DiffEditorOptions"/>. Assign a new instance to
    /// <see cref="DiffEditorOptions.HideUnchangedRegions"/> to push a change to Monaco.
    /// </remarks>
    public sealed partial class DiffEditorHideUnchangedRegionsOptions
    {
        /// <summary>
        /// Collapse unchanged regions. Defaults to false.
        /// </summary>
        [JsonInclude]
        public bool? Enabled { get; set; }

        /// <summary>
        /// Number of lines revealed when a collapsed region is expanded.
        /// </summary>
        [JsonInclude]
        public uint? RevealLineCount { get; set; }

        /// <summary>
        /// Smallest run of unchanged lines that may be collapsed.
        /// </summary>
        [JsonInclude]
        public uint? MinimumLineCount { get; set; }

        /// <summary>
        /// Number of unchanged lines kept visible on either side of a change.
        /// </summary>
        [JsonInclude]
        public uint? ContextLineCount { get; set; }
    }
}
