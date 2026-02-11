using System.Text.Json.Serialization;

namespace Monaco.Helpers
{
    [JsonConverter(typeof(JsonStringEnumConverter<TextDecoration>))]
    public enum TextDecoration
    {
        [JsonStringEnumMemberName("none")]
        None,
        [JsonStringEnumMemberName("underline")]
        Underline,
        [JsonStringEnumMemberName("overline")]
        Overline,
        [JsonStringEnumMemberName("line-through")]
        LineThrough,
        [JsonStringEnumMemberName("initial")]
        Initial,
        [JsonStringEnumMemberName("inherit")]
        Inherit,
    }
}
