using Monaco;
using Monaco.Editor;
using Newtonsoft.Json;

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
        [JsonProperty("command", NullValueHandling = NullValueHandling.Ignore)]
        public Command? Command { get; set; }

        /// <summary>
        /// Gets or sets the diagnostics that this code action resolves.
        /// </summary>
        [JsonProperty("diagnostics", NullValueHandling = NullValueHandling.Ignore)]
        public IMarkerData[]? Diagnostics { get; set; }

        /// <summary>
        /// Gets or sets the reason why this code action is disabled.
        /// </summary>
        [JsonProperty("disabled", NullValueHandling = NullValueHandling.Ignore)]
        public string? Disabled { get; set; }

        /// <summary>
        /// Gets or sets the workspace edit to apply when this code action is selected.
        /// </summary>
        [JsonProperty("edit", NullValueHandling = NullValueHandling.Ignore)]
        public WorkspaceEdit? Edit { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a preferred code action.
        /// </summary>
        [JsonProperty("isPreferred", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsPreferred { get; set; }

        /// <summary>
        /// Gets or sets the kind of code action (e.g., "quickfix", "refactor").
        /// </summary>
        [JsonProperty("kind", NullValueHandling = NullValueHandling.Ignore)]
        public string? Kind { get; set; }

        /// <summary>
        /// Gets or sets the title of this code action displayed in the UI.
        /// </summary>
        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string? Title { get; set; }
    }
}

