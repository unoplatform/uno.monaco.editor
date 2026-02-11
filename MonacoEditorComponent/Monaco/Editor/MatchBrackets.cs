using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Enable highlighting of matching brackets.
    /// Defaults to 'always'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<MatchBrackets>))]
    public enum MatchBrackets
    {
        [JsonStringEnumMemberName("always")]
        Always,
        [JsonStringEnumMemberName("near")]
        Near,
        [JsonStringEnumMemberName("never")]
        Never,
    };
}
