using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Render horizontal or vertical scrollbar.
    /// Defaults to 'auto'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ScrollbarBehavior>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum ScrollbarBehavior
    {
        [JsonStringEnumMemberName("auto")]
        [EnumMember(Value = "auto")]
        Auto,
        [JsonStringEnumMemberName("hidden")]
        [EnumMember(Value = "hidden")]
        Hidden,
        [JsonStringEnumMemberName("visible")]
        [EnumMember(Value = "visible")]
        Visible,
    };
}
