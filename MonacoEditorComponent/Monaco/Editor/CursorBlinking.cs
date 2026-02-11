using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Control the cursor animation style, possible values are 'blink', 'smooth', 'phase',
    /// 'expand' and 'solid'.
    /// Defaults to 'blink'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<CursorBlinking>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum CursorBlinking
    {
        [JsonStringEnumMemberName("blink")]
        [EnumMember(Value = "blink")]
        Blink,
        [JsonStringEnumMemberName("expand")]
        [EnumMember(Value = "expand")]
        Expand,
        [JsonStringEnumMemberName("phase")]
        [EnumMember(Value = "phase")]
        Phase,
        [JsonStringEnumMemberName("smooth")]
        [EnumMember(Value = "smooth")]
        Smooth,
        [JsonStringEnumMemberName("solid")]
        [EnumMember(Value = "solid")]
        Solid,
    };
}
