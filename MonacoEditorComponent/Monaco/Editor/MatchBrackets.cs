using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Enable highlighting of matching brackets.
    /// Defaults to 'always'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<MatchBrackets>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum MatchBrackets
    {
        [JsonStringEnumMemberName("always")]
        [EnumMember(Value = "always")]
        Always,
        [JsonStringEnumMemberName("near")]
        [EnumMember(Value = "near")]
        Near,
        [JsonStringEnumMemberName("never")]
        [EnumMember(Value = "never")]
        Never,
    };
}
