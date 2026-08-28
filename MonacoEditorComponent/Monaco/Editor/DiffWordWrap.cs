using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Control the wrapping of the diff editor.
    /// When "inherit", the wrapping of the underlying editor options is used.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<DiffWordWrap>))]
    public enum DiffWordWrap
    {
        [JsonStringEnumMemberName("inherit")]
        [EnumMember(Value = "inherit")]
        Inherit,
        [JsonStringEnumMemberName("off")]
        [EnumMember(Value = "off")]
        Off,
        [JsonStringEnumMemberName("on")]
        [EnumMember(Value = "on")]
        On,
    };
}
