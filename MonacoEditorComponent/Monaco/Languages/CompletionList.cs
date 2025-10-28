using Newtonsoft.Json;

namespace Monaco.Languages
{
    /// <summary>
    /// Represents a list of completion items provided by a completion provider.
    /// </summary>
    public sealed class CompletionList
    {
        /// <summary>
        /// Gets or sets a value indicating whether this list is incomplete and should be re-queried as the user continues typing.
        /// </summary>
        [JsonProperty("incomplete", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Incomplete { get; set; }

        /// <summary>
        /// Gets or sets the array of completion suggestions.
        /// </summary>
        [JsonProperty("suggestions")]
        public CompletionItem[]? Suggestions { get; set; }
    }
}
