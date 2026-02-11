using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Accept suggestions on ENTER.
    /// Defaults to 'on'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AcceptSuggestionOnEnter>))]
    public enum AcceptSuggestionOnEnter
    {
        [JsonStringEnumMemberName("off")]
        Off,
        [JsonStringEnumMemberName("on")]
        On,
        [JsonStringEnumMemberName("smart")]
        Smart,
    };
}
