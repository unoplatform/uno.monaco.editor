using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Controls if Find in Selection flag is turned on in the editor.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AutoFindInSelection>))]
    public enum AutoFindInSelection
    {
        [JsonStringEnumMemberName("always")]
        Always,
        [JsonStringEnumMemberName("multiline")]
        Multiline,
        [JsonStringEnumMemberName("never")]
        Never,
    };
}
