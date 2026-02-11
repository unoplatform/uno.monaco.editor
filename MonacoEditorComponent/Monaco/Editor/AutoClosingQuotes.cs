using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Options for auto closing quotes.
    /// Defaults to language defined behavior.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AutoClosingQuotes>))]
    public enum AutoClosingQuotes
    {
        [JsonStringEnumMemberName("always")]
        Always,
        [JsonStringEnumMemberName("beforeWhitespace")]
        BeforeWhitespace,
        [JsonStringEnumMemberName("languageDefined")]
        LanguageDefined,
        [JsonStringEnumMemberName("never")]
        Never,
    };
}
