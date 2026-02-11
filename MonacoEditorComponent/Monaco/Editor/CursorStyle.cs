using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Control the cursor style, either 'block' or 'line'.
    /// Defaults to 'line'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<CursorStyle>))]
    public enum CursorStyle
    {
        [JsonStringEnumMemberName("block")]
        Block,
        [JsonStringEnumMemberName("block-outline")]
        BlockOutline,
        [JsonStringEnumMemberName("line")]
        Line,
        [JsonStringEnumMemberName("line-thin")]
        LineThin,
        [JsonStringEnumMemberName("underline")]
        Underline,
        [JsonStringEnumMemberName("underline-thin")]
        UnderlineThin,
    };
}
