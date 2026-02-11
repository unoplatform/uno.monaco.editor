using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Options for auto surrounding.
    /// Defaults to always allowing auto surrounding.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AutoSurround>))]
    public enum AutoSurround
    {
        [JsonStringEnumMemberName("brackets")]
        Brackets,
        [JsonStringEnumMemberName("languageDefined")]
        LanguageDefined,
        [JsonStringEnumMemberName("never")]
        Never,
        [JsonStringEnumMemberName("quotes")]
        Quotes,
    };
}
