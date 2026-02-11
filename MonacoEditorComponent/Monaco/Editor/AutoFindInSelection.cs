using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Controls if Find in Selection flag is turned on in the editor.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AutoFindInSelection>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum AutoFindInSelection
    {
        [JsonStringEnumMemberName("always")]
        [EnumMember(Value = "always")]
        Always,
        [JsonStringEnumMemberName("multiline")]
        [EnumMember(Value = "multiline")]
        Multiline,
        [JsonStringEnumMemberName("never")]
        [EnumMember(Value = "never")]
        Never,
    };
}
