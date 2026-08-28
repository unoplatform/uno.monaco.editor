#nullable enable
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Experimental diff editor options. These track the upstream Monaco
    /// <c>IDiffEditorBaseOptions.experimental</c> bag and may change between Monaco versions.
    /// </summary>
    /// <remarks>
    /// This is a plain value object: mutating it in place does not raise
    /// <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> on the owning
    /// <see cref="DiffEditorOptions"/>. Assign a new instance to
    /// <see cref="DiffEditorOptions.Experimental"/> to push a change to Monaco.
    /// </remarks>
    public sealed partial class DiffEditorExperimentalOptions
    {
        /// <summary>
        /// Show moved code blocks as moves rather than as a delete plus an insert.
        /// Defaults to false.
        /// </summary>
        [JsonInclude]
        public bool? ShowMoves { get; set; }

        /// <summary>
        /// Render decorations for regions that produced no visible change.
        /// </summary>
        [JsonInclude]
        public bool? ShowEmptyDecorations { get; set; }

        /// <summary>
        /// Use the true inline view. Only applies when
        /// <see cref="DiffEditorOptions.RenderSideBySide"/> is <see langword="false"/>.
        /// </summary>
        [JsonInclude]
        public bool? UseTrueInlineView { get; set; }
    }
}
