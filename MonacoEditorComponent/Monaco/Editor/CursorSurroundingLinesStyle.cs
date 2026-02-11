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
    public enum CursorSurroundingLinesStyle
    {
        [JsonStringEnumMemberName("all")]
        All,
        [JsonStringEnumMemberName("default")]
        Default,
    };
}
