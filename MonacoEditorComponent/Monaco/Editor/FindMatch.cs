using Monaco.Helpers;
using Newtonsoft.Json;
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
        [JsonProperty("matches", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string[]? Matches { get; set; }

        /// <summary>
        /// Gets or sets the range where the match was found.
        /// </summary>
        [JsonProperty("range", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonConverter(typeof(InterfaceToClassConverter<IRange, Range>))]
        [Newtonsoft.Json.JsonConverter(typeof(NewtonsoftInterfaceToClassConverter<IRange, Range>))]
        public IRange? Range { get; set; }
    }
}
