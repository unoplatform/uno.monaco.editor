using Newtonsoft.Json;

namespace Monaco.Editor
{
    /// <summary>
    /// Represents related diagnostic information for a marker, providing additional context about the issue.
    /// </summary>
    public sealed class IRelatedInformation
    {
        /// <summary>
        /// Gets or sets the end column of the related information range.
        /// </summary>
        [JsonProperty("endColumn")]
        public uint EndColumn { get; set; }

        /// <summary>
        /// Gets or sets the end line number of the related information range.
        /// </summary>
        [JsonProperty("endLineNumber")]
        public uint EndLineNumber { get; set; }

        /// <summary>
        /// Gets or sets the message describing the related information.
        /// </summary>
        [JsonProperty("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the URI of the resource where the related information is located.
        /// </summary>
        [JsonProperty("resource")]
        public Uri? Resource { get; set; }

        /// <summary>
        /// Gets or sets the start column of the related information range.
        /// </summary>
        [JsonProperty("startColumn")]
        public uint StartColumn { get; set; }

        /// <summary>
        /// Gets or sets the start line number of the related information range.
        /// </summary>
        [JsonProperty("startLineNumber")]
        public uint StartLineNumber { get; set; }
    }
}