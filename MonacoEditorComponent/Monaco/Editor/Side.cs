using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Control the side of the minimap in editor.
    /// Defaults to 'right'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<Side>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum Side
    {
        [JsonStringEnumMemberName("left")]
        [EnumMember(Value = "left")]
        Left,
        [JsonStringEnumMemberName("right")]
        [EnumMember(Value = "right")]
        Right,
    };
}
