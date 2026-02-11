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
        Insert,
        [JsonStringEnumMemberName("replace")]
        Replace,
    };
}
