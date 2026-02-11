using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// The history mode for suggestions.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SuggestSelection>))]
    public enum SuggestSelection
    {
        [JsonStringEnumMemberName("first")]
        First,
        [JsonStringEnumMemberName("recentlyUsed")]
        RecentlyUsed,
        [JsonStringEnumMemberName("recentlyUsedByPrefix")]
        RecentlyUsedByPrefix,
    };
}
