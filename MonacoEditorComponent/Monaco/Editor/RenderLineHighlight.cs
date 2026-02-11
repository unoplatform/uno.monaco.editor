using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Enable rendering of current line highlight.
    /// Defaults to all.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RenderLineHighlight>))]
    public enum RenderLineHighlight
    {
        [JsonStringEnumMemberName("all")]
        [EnumMember(Value = "all")]
        All,
        [JsonStringEnumMemberName("gutter")]
        [EnumMember(Value = "gutter")]
        Gutter,
        [JsonStringEnumMemberName("line")]
        [EnumMember(Value = "line")]
        Line,
        [JsonStringEnumMemberName("none")]
        [EnumMember(Value = "none")]
        None,
    };
}
