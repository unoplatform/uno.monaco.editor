using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Enable rendering of whitespace.
    /// Defaults to none.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RenderWhitespace>))]
    public enum RenderWhitespace
    {
        [JsonStringEnumMemberName("all")]
        [EnumMember(Value = "all")]
        All,
        [JsonStringEnumMemberName("boundary")]
        [EnumMember(Value = "boundary")]
        Boundary,
        [JsonStringEnumMemberName("none")]
        [EnumMember(Value = "none")]
        None,
        [JsonStringEnumMemberName("selection")]
        [EnumMember(Value = "selection")]
        Selection,
    };
}
