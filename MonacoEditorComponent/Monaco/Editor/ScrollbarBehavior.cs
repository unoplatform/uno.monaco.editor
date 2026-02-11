using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Render horizontal or vertical scrollbar.
    /// Defaults to 'auto'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ScrollbarBehavior>))]
    public enum ScrollbarBehavior
    {
        [JsonStringEnumMemberName("auto")]
        Auto,
        [JsonStringEnumMemberName("hidden")]
        Hidden,
        [JsonStringEnumMemberName("visible")]
        Visible,
    };
}
