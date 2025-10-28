using Newtonsoft.Json;

namespace Monaco.Languages
{
    /// <summary>
    /// Represents a collection of edits to be applied across multiple files in the workspace.
    /// </summary>
    public sealed class WorkspaceEdit
    {
        /// <summary>
        /// Gets or sets the array of text edits to apply.
        /// </summary>
        [JsonProperty("edits", NullValueHandling = NullValueHandling.Ignore)]
        public WorkspaceTextEdit[]? Edits { get; set; } // TODO: This could also be of type 'WorkspaceFileEdit'
    }
}