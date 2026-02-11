using System.Text.Json.Serialization;

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
        public uint EndColumn { get; set; }

        /// <summary>
        /// Gets or sets the end line number of the related information range.
        /// </summary>
        public uint EndLineNumber { get; set; }

        /// <summary>
        /// Gets or sets the message describing the related information.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the URI of the resource where the related information is located.
        /// </summary>
        public Uri? Resource { get; set; }

        /// <summary>
        /// Gets or sets the start column of the related information range.
        /// </summary>
        public uint StartColumn { get; set; }

        /// <summary>
        /// Gets or sets the start line number of the related information range.
        /// </summary>
        public uint StartLineNumber { get; set; }
    }
}