using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Enable rendering of whitespace.
    /// Defaults to none.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RenderWhitespace>))]
    public enum RenderWhitespace
    {
        [JsonStringEnumMemberName("all")]
        All,
        [JsonStringEnumMemberName("boundary")]
        Boundary,
        [JsonStringEnumMemberName("none")]
        None,
        [JsonStringEnumMemberName("selection")]
        Selection,
    };
}
