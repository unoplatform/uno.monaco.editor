using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Enable tab completion.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<TabCompletion>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum TabCompletion
    {
        [JsonStringEnumMemberName("off")]
        [EnumMember(Value = "off")]
        Off,
        [JsonStringEnumMemberName("on")]
        [EnumMember(Value = "on")]
        On,
        [JsonStringEnumMemberName("onlySnippets")]
        [EnumMember(Value = "onlySnippets")]
        OnlySnippets,
    };
}
