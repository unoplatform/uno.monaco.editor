using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Helpers
{
    [JsonConverter(typeof(JsonStringEnumConverter<TextDecoration>))]
    public enum TextDecoration
    {
        [JsonStringEnumMemberName("none")]
        [EnumMember(Value = "none")]
        None,
        [JsonStringEnumMemberName("underline")]
        [EnumMember(Value = "underline")]
        Underline,
        [JsonStringEnumMemberName("overline")]
        [EnumMember(Value = "overline")]
        Overline,
        [JsonStringEnumMemberName("line-through")]
        [EnumMember(Value = "line-through")]
        LineThrough,
        [JsonStringEnumMemberName("initial")]
        [EnumMember(Value = "initial")]
        Initial,
        [JsonStringEnumMemberName("inherit")]
        [EnumMember(Value = "inherit")]
        Inherit,
    }
}
