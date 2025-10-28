using Monaco;
using Monaco.Editor;
using Newtonsoft.Json;

namespace Monaco.Languages
{
    /// <summary>
    /// Represents a text edit operation to be applied to the document.
    /// </summary>
    public sealed class TextEdit
    {
        /// <summary>
        /// Gets or sets the range of text to be replaced.
        /// </summary>
        [JsonProperty("range", NullValueHandling = NullValueHandling.Ignore)]
        public IRange? Range { get; set; }

        /// <summary>
        /// Gets or sets the new text to insert.
        /// </summary>
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string? Text { get; set; }

        /// <summary>
        /// Gets or sets the end-of-line sequence to use.
        /// </summary>
        [JsonProperty("eol", NullValueHandling = NullValueHandling.Ignore)]
        public EndOfLineSequence Eol { get; set; }
    }
}