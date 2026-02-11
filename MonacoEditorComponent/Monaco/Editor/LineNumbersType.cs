using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    [JsonConverter(typeof(JsonStringEnumConverter<LineNumbersType>))]
    public enum LineNumbersType
    {
        [JsonStringEnumMemberName("interval")]
        Interval,
        [JsonStringEnumMemberName("off")]
        Off,
        [JsonStringEnumMemberName("on")]
        On,
        [JsonStringEnumMemberName("relative")]
        Relative,
    };
}
