using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Options for typing over closing quotes or brackets.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AutoClosingOvertype>))]
    public enum AutoClosingOvertype
    {
        [JsonStringEnumMemberName("always")]
        [EnumMember(Value = "always")]
        Always,
        [JsonStringEnumMemberName("auto")]
        [EnumMember(Value = "auto")]
        Auto,
        [JsonStringEnumMemberName("never")]
        [EnumMember(Value = "never")]
        Never,
    };
}
