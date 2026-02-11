using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Enable snippet suggestions. Default to 'true'.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SnippetSuggestions>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum SnippetSuggestions
    {
        [JsonStringEnumMemberName("bottom")]
        [EnumMember(Value = "bottom")]
        Bottom,
        [JsonStringEnumMemberName("inline")]
        [EnumMember(Value = "inline")]
        Inline,
        [JsonStringEnumMemberName("none")]
        [EnumMember(Value = "none")]
        None,
        [JsonStringEnumMemberName("top")]
        [EnumMember(Value = "top")]
        Top,
    };
}
