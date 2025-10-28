using Newtonsoft.Json;

namespace Monaco.Languages
{
    /// <summary>
    /// Represents a code lens that displays actionable information inline in the editor.
    /// </summary>
    public sealed class CodeLens
    {
        /// <summary>
        /// Gets or sets the command associated with this code lens.
        /// </summary>
        [JsonProperty("command", NullValueHandling = NullValueHandling.Ignore)]
        public Command? Command { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for this code lens.
        /// </summary>
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the range where this code lens should be displayed.
        /// </summary>
        [JsonProperty("range", NullValueHandling = NullValueHandling.Ignore)]
        public IRange? Range { get; set; }
    }
}

