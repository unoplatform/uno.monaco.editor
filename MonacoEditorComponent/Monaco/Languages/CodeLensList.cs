namespace Monaco.Languages
{
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a list of code lenses provided by a code lens provider.
    /// </summary>
    public sealed class CodeLensList // IDisposible?
    {
        /// <summary>
        /// Gets or sets the array of code lenses.
        /// </summary>
        [JsonProperty("lenses", NullValueHandling = NullValueHandling.Ignore)]
        public CodeLens[]? Lenses { get; set; }
    }
}

