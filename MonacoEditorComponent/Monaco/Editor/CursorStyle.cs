using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Control the cursor style, either 'block' or 'line'.
    /// Defaults to 'line'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<CursorStyle>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum CursorStyle
    {
        [JsonStringEnumMemberName("block")]
        [EnumMember(Value = "block")]
        Block,
        [JsonStringEnumMemberName("block-outline")]
        [EnumMember(Value = "block-outline")]
        BlockOutline,
        [JsonStringEnumMemberName("line")]
        [EnumMember(Value = "line")]
        Line,
        [JsonStringEnumMemberName("line-thin")]
        [EnumMember(Value = "line-thin")]
        LineThin,
        [JsonStringEnumMemberName("underline")]
        [EnumMember(Value = "underline")]
        Underline,
        [JsonStringEnumMemberName("underline-thin")]
        [EnumMember(Value = "underline-thin")]
        UnderlineThin,
    };
}
