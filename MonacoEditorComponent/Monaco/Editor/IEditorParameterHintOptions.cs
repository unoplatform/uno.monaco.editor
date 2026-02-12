using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Configuration options for parameter hints
    /// </summary>
    public sealed class IEditorParameterHintOptions
    {
        /// <summary>
        /// Enable cycling of parameter hints.
        /// Defaults to false.
        /// </summary>
        public bool? Cycle { get; set; }

        /// <summary>
        /// Enable parameter hints.
        /// Defaults to true.
        /// </summary>
        public bool? Enabled { get; set; }
    }

}
