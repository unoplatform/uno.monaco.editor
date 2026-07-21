using System.Runtime.Serialization;
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
        [EnumMember(Value = "always")]
        Always,
        [JsonStringEnumMemberName("beforeWhitespace")]
        [EnumMember(Value = "beforeWhitespace")]
        BeforeWhitespace,
        [JsonStringEnumMemberName("languageDefined")]
        [EnumMember(Value = "languageDefined")]
        LanguageDefined,
        [JsonStringEnumMemberName("never")]
        [EnumMember(Value = "never")]
        Never,
    };
}
