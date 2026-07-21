using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Configure the behaviour when pasting a text with the line count equal to the cursor
    /// count.
    /// Defaults to 'spread'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<MultiCursorPaste>))]
    public enum MultiCursorPaste
    {
        [JsonStringEnumMemberName("full")]
        [EnumMember(Value = "full")]
        Full,
        [JsonStringEnumMemberName("spread")]
        [EnumMember(Value = "spread")]
        Spread,
    };
}
