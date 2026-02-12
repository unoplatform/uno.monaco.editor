using Monaco.Helpers;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Represents a match found by the editor's find functionality.
    /// </summary>
    public sealed class FindMatch
    {
        /// <summary>
        /// Gets or sets the matched strings.
        /// </summary>
        public string[]? Matches { get; set; }

        /// <summary>
        /// Gets or sets the range where the match was found.
        /// </summary>
        [JsonConverter(typeof(InterfaceToClassConverter<IRange, Range>))]
        public IRange? Range { get; set; }
    }
}
