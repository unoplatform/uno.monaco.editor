using Monaco.Editor;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Configuration options for the diff editor.
    /// </summary>
    public interface IDiffEditorOptions : IEditorOptions
    {
        /// <summary>
        /// Allow the user to resize the diff editor split view.
        /// Defaults to true.
        /// </summary>
        bool? EnableSplitViewResizing { get; set; }
        /// <summary>
        /// Compute the diff by ignoring leading/trailing whitespace
        /// Defaults to true.
        /// </summary>
        bool? IgnoreTrimWhitespace { get; set; }
        /// <summary>
        /// Timeout in milliseconds after which diff computation is cancelled.
        /// Defaults to 5000.
        /// </summary>
        uint? MaxComputationTime { get; set; }
        /// <summary>
        /// Original model should be editable?
        /// Defaults to false.
        /// </summary>
        bool? OriginalEditable { get; set; }
        /// <summary>
        /// Render +/- indicators for added/deleted changes.
        /// Defaults to true.
        /// </summary>
        bool? RenderIndicators { get; set; }
        /// <summary>
        /// Render the differences in two side-by-side editors.
        /// Defaults to true.
        /// </summary>
        bool? RenderSideBySide { get; set; }
    }
}
