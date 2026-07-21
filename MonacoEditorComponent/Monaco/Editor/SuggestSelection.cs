using System.Runtime.Serialization;
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
        [EnumMember(Value = "first")]
        First,
        [JsonStringEnumMemberName("recentlyUsed")]
        [EnumMember(Value = "recentlyUsed")]
        RecentlyUsed,
        [JsonStringEnumMemberName("recentlyUsedByPrefix")]
        [EnumMember(Value = "recentlyUsedByPrefix")]
        RecentlyUsedByPrefix,
    };
}
