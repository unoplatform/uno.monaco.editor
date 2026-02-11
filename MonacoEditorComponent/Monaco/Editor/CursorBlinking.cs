using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Control the cursor animation style, possible values are 'blink', 'smooth', 'phase',
    /// 'expand' and 'solid'.
    /// Defaults to 'blink'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<CursorBlinking>))]
    public enum CursorBlinking
    {
        [JsonStringEnumMemberName("blink")]
        Blink,
        [JsonStringEnumMemberName("expand")]
        Expand,
        [JsonStringEnumMemberName("phase")]
        Phase,
        [JsonStringEnumMemberName("smooth")]
        Smooth,
        [JsonStringEnumMemberName("solid")]
        Solid,
    };
}
