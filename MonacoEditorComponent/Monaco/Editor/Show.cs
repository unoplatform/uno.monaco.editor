using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Control the rendering of the minimap slider.
    /// Defaults to 'mouseover'.
    ///
    /// Controls whether the fold actions in the gutter stay always visible or hide unless the
    /// mouse is over the gutter.
    /// Defaults to 'mouseover'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<Show>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum Show
    {
        [JsonStringEnumMemberName("always")]
        [EnumMember(Value = "always")]
        Always,
        [JsonStringEnumMemberName("mouseover")]
        [EnumMember(Value = "mouseover")]
        Mouseover,
    };
}
