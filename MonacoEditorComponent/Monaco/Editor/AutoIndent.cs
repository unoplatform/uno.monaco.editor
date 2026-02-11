using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Enable auto indentation adjustment.
    /// Defaults to false.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AutoIndent>))]
    public enum AutoIndent
    {
        [JsonStringEnumMemberName("advanced")]
        Advanced,
        [JsonStringEnumMemberName("brackets")]
        Brackets,
        [JsonStringEnumMemberName("full")]
        Full,
        [JsonStringEnumMemberName("keep")]
        Keep,
        [JsonStringEnumMemberName("none")]
        None,
    };
}
