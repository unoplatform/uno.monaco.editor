using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Controls when `cursorSurroundingLines` should be enforced
    /// Defaults to `default`, `cursorSurroundingLines` is not enforced when cursor position is
    /// changed
    /// by mouse.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<CursorSurroundingLinesStyle>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum CursorSurroundingLinesStyle
    {
        [JsonStringEnumMemberName("all")]
        [EnumMember(Value = "all")]
        All,
        [JsonStringEnumMemberName("default")]
        [EnumMember(Value = "default")]
        Default,
    };
}
