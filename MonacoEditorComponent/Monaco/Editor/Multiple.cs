using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    [JsonConverter(typeof(JsonStringEnumConverter<Multiple>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum Multiple
    {
        [JsonStringEnumMemberName("goto")]
        [EnumMember(Value = "goto")]
        Goto,
        [JsonStringEnumMemberName("gotoAndPeek")]
        [EnumMember(Value = "gotoAndPeek")]
        GotoAndPeek,
        [JsonStringEnumMemberName("peek")]
        [EnumMember(Value = "peek")]
        Peek,
    };
}
