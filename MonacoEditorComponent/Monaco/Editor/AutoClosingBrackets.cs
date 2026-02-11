using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Options for auto closing brackets.
    /// Defaults to language defined behavior.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AutoClosingBrackets>))]
    public enum AutoClosingBrackets
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
