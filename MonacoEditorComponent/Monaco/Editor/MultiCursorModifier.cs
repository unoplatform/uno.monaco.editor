using System.Runtime.Serialization;
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
        [EnumMember(Value = "alt")]
        Alt,
        [JsonStringEnumMemberName("ctrlCmd")]
        [EnumMember(Value = "ctrlCmd")]
        CtrlCmd,
    };
}
