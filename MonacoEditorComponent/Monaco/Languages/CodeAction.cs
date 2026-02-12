using Monaco;
using Monaco.Editor;
using System.Text.Json.Serialization;

namespace Monaco.Languages
{
    /// <summary>
    /// Represents a code action that can be performed in the editor, such as a quick fix or refactoring.
    /// </summary>
    public sealed class CodeAction
    {
        /// <summary>
        /// Gets or sets the command to execute when this code action is selected.
        /// </summary>
        public Command? Command { get; set; }

        /// <summary>
        /// Gets or sets the diagnostics that this code action resolves.
        /// </summary>
        public IMarkerData[]? Diagnostics { get; set; }

        /// <summary>
        /// Gets or sets the reason why this code action is disabled.
        /// </summary>
        public string? Disabled { get; set; }

        /// <summary>
        /// Gets or sets the workspace edit to apply when this code action is selected.
        /// </summary>
        public WorkspaceEdit? Edit { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a preferred code action.
        /// </summary>
        public bool IsPreferred { get; set; }

        /// <summary>
        /// Gets or sets the kind of code action (e.g., "quickfix", "refactor").
        /// </summary>
        public string? Kind { get; set; }

        /// <summary>
        /// Gets or sets the title of this code action displayed in the UI.
        /// </summary>
        public string? Title { get; set; }
    }
}

