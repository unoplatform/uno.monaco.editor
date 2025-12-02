using Monaco;
using Newtonsoft.Json;

namespace Monaco.Languages
{
    /// <summary>
    /// Represents a text edit in a workspace edit, targeting a specific resource.
    /// </summary>
    public sealed class WorkspaceTextEdit
    {
        /// <summary>
        /// Gets or sets the text edit to apply.
        /// </summary>
        [JsonProperty("edit", NullValueHandling = NullValueHandling.Ignore)]
        public TextEdit? Edit { get; set; }

        /// <summary>
        /// Gets or sets the metadata associated with this edit.
        /// </summary>
        [JsonProperty("metadata", NullValueHandling = NullValueHandling.Ignore)]
        public WorkspaceEditMetadata? Metadata { get; set; }

        /// <summary>
        /// Gets or sets the model version ID to ensure the edit is applied to the correct version.
        /// Important for this to be nullable here, as otherwise it still serializes as '0' which throws a 'bad state - model changed in the meantime' as version will mismatch.
        /// </summary>
        [JsonProperty("modelVersionId", NullValueHandling = NullValueHandling.Ignore)]
        public double? ModelVersionId { get; set; }

        /// <summary>
        /// Gets or sets the URI of the resource to edit.
        /// </summary>
        [JsonProperty("resource", NullValueHandling = NullValueHandling.Ignore)]
        public Uri? Resource { get; set; }
    }
}
