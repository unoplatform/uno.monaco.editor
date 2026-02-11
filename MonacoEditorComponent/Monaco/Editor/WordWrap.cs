using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Control the wrapping of the editor.
    /// When `wordWrap` = "off", the lines will never wrap.
    /// When `wordWrap` = "on", the lines will wrap at the viewport width.
    /// When `wordWrap` = "wordWrapColumn", the lines will wrap at `wordWrapColumn`.
    /// When `wordWrap` = "bounded", the lines will wrap at min(viewport width, wordWrapColumn).
    /// Defaults to "off".
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<WordWrap>))]
    public enum WordWrap
    {
        [JsonStringEnumMemberName("bounded")]
        [EnumMember(Value = "bounded")]
        Bounded,
        [JsonStringEnumMemberName("off")]
        [EnumMember(Value = "off")]
        Off,
        [JsonStringEnumMemberName("on")]
        [EnumMember(Value = "on")]
        On,
        [JsonStringEnumMemberName("wordWrapColumn")]
        [EnumMember(Value = "wordWrapColumn")]
        WordWrapColumn,
    };
}
