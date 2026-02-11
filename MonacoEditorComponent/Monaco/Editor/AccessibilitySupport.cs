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
        Auto,
        [JsonStringEnumMemberName("off")]
        Off,
        [JsonStringEnumMemberName("on")]
        On,
    };
}
