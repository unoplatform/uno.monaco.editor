using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Enable snippet suggestions. Default to 'true'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SnippetSuggestions>))]
    public enum SnippetSuggestions
    {
        [JsonStringEnumMemberName("bottom")]
        Bottom,
        [JsonStringEnumMemberName("inline")]
        Inline,
        [JsonStringEnumMemberName("none")]
        None,
        [JsonStringEnumMemberName("top")]
        Top,
    };
}
