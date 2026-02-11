using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    [JsonConverter(typeof(JsonStringEnumConverter<Multiple>))]
    public enum Multiple
    {
        [JsonStringEnumMemberName("goto")]
        Goto,
        [JsonStringEnumMemberName("gotoAndPeek")]
        GotoAndPeek,
        [JsonStringEnumMemberName("peek")]
        Peek,
    };
}
