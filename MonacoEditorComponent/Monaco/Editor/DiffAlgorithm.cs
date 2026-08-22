using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Selects the algorithm the diff editor uses to compute changes.
    /// Defaults to "advanced".
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<DiffAlgorithm>))]
    public enum DiffAlgorithm
    {
        [JsonStringEnumMemberName("advanced")]
        [EnumMember(Value = "advanced")]
        Advanced,
        [JsonStringEnumMemberName("legacy")]
        [EnumMember(Value = "legacy")]
        Legacy,
    };
}
