using Monaco.Helpers;
using Newtonsoft.Json;

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
        [JsonProperty("matches", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string[]? Matches { get; set; }

        /// <summary>
        /// Gets or sets the range where the match was found.
        /// </summary>
        [JsonProperty("range", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [JsonConverter(typeof(InterfaceToClassConverter<IRange, Range>))]
        public IRange? Range { get; set; }
    }
}

