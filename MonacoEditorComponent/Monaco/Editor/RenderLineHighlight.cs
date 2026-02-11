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
        All,
        [JsonStringEnumMemberName("gutter")]
        Gutter,
        [JsonStringEnumMemberName("line")]
        Line,
        [JsonStringEnumMemberName("none")]
        None,
    };
}
