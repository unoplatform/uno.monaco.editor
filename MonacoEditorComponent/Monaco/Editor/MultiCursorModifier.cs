using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// The modifier to be used to add multiple cursors with the mouse.
    /// Defaults to 'alt'
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<MultiCursorModifier>))]
    public enum MultiCursorModifier
    {
        [JsonStringEnumMemberName("alt")]
        Alt,
        [JsonStringEnumMemberName("ctrlCmd")]
        CtrlCmd,
    };
}
