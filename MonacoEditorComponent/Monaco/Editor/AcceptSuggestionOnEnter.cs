using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Accept suggestions on ENTER.
    /// Defaults to 'on'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AcceptSuggestionOnEnter>))]
    public enum AcceptSuggestionOnEnter
    {
        [JsonStringEnumMemberName("off")]
        [EnumMember(Value = "off")]
        Off,
        [JsonStringEnumMemberName("on")]
        [EnumMember(Value = "on")]
        On,
        [JsonStringEnumMemberName("smart")]
        [EnumMember(Value = "smart")]
        Smart,
    };
}
