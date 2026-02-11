using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Enable tab completion.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<TabCompletion>))]
    public enum TabCompletion
    {
        [JsonStringEnumMemberName("off")]
        Off,
        [JsonStringEnumMemberName("on")]
        On,
        [JsonStringEnumMemberName("onlySnippets")]
        OnlySnippets,
    };
}
