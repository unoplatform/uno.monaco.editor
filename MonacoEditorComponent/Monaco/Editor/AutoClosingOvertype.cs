using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Options for typing over closing quotes or brackets.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AutoClosingOvertype>))]
    public enum AutoClosingOvertype
    {
        [JsonStringEnumMemberName("always")]
        Always,
        [JsonStringEnumMemberName("auto")]
        Auto,
        [JsonStringEnumMemberName("never")]
        Never,
    };
}
