using System.Runtime.Serialization;
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
        [EnumMember(Value = "brackets")]
        Brackets,
        [JsonStringEnumMemberName("languageDefined")]
        [EnumMember(Value = "languageDefined")]
        LanguageDefined,
        [JsonStringEnumMemberName("never")]
        [EnumMember(Value = "never")]
        Never,
        [JsonStringEnumMemberName("quotes")]
        [EnumMember(Value = "quotes")]
        Quotes,
    };
}
