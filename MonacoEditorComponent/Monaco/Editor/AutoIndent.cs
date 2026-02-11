using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Enable auto indentation adjustment.
    /// Defaults to false.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AutoIndent>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum AutoIndent
    {
        [JsonStringEnumMemberName("advanced")]
        [EnumMember(Value = "advanced")]
        Advanced,
        [JsonStringEnumMemberName("brackets")]
        [EnumMember(Value = "brackets")]
        Brackets,
        [JsonStringEnumMemberName("full")]
        [EnumMember(Value = "full")]
        Full,
        [JsonStringEnumMemberName("keep")]
        [EnumMember(Value = "keep")]
        Keep,
        [JsonStringEnumMemberName("none")]
        [EnumMember(Value = "none")]
        None,
    };
}
