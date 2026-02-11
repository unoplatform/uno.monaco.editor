using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Control the mouse pointer style, either 'text' or 'default' or 'copy'
    /// Defaults to 'text'
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<MouseStyle>))]
    public enum MouseStyle
    {
        [JsonStringEnumMemberName("copy")]
        Copy,
        [JsonStringEnumMemberName("default")]
        Default,
        [JsonStringEnumMemberName("text")]
        Text,
    };
}
