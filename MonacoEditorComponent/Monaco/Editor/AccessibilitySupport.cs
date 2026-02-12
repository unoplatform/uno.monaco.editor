using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Configure the editor's accessibility support.
    /// Defaults to 'auto'. It is best to leave this to 'auto'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AccessibilitySupport>))]
    public enum AccessibilitySupport
    {
        [JsonStringEnumMemberName("auto")]
        [EnumMember(Value = "auto")]
        Auto,
        [JsonStringEnumMemberName("off")]
        [EnumMember(Value = "off")]
        Off,
        [JsonStringEnumMemberName("on")]
        [EnumMember(Value = "on")]
        On,
    };
}
