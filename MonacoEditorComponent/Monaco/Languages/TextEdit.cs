using Monaco;
using Monaco.Editor;
using System.Text.Json.Serialization;

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
        public IRange? Range { get; set; }

        /// <summary>
        /// Gets or sets the new text to insert.
        /// </summary>
        public string? Text { get; set; }

        /// <summary>
        /// Gets or sets the end-of-line sequence to use.
        /// </summary>
        public EndOfLineSequence Eol { get; set; }
    }
}