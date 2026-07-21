using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    [JsonConverter(typeof(JsonStringEnumConverter<LineNumbersType>))]
    public enum LineNumbersType
    {
        [JsonStringEnumMemberName("interval")]
        [EnumMember(Value = "interval")]
        Interval,
        [JsonStringEnumMemberName("off")]
        [EnumMember(Value = "off")]
        Off,
        [JsonStringEnumMemberName("on")]
        [EnumMember(Value = "on")]
        On,
        [JsonStringEnumMemberName("relative")]
        [EnumMember(Value = "relative")]
        Relative,
    };
}
