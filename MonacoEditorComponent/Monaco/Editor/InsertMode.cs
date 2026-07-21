using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Overwrite word ends on accept. Default to false.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<InsertMode>))]
    public enum InsertMode
    {
        [JsonStringEnumMemberName("insert")]
        [EnumMember(Value = "insert")]
        Insert,
        [JsonStringEnumMemberName("replace")]
        [EnumMember(Value = "replace")]
        Replace,
    };
}
