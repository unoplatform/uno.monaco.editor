using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Control indentation of wrapped lines. Can be: 'none', 'same', 'indent' or 'deepIndent'.
    /// Defaults to 'same' in vscode and to 'none' in monaco-editor.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<WrappingIndent>))]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum WrappingIndent
    {
        [JsonStringEnumMemberName("deepIndent")]
        [EnumMember(Value = "deepIndent")]
        DeepIndent,
        [JsonStringEnumMemberName("indent")]
        [EnumMember(Value = "indent")]
        Indent,
        [JsonStringEnumMemberName("none")]
        [EnumMember(Value = "none")]
        None,
        [JsonStringEnumMemberName("same")]
        [EnumMember(Value = "same")]
        Same,
    };
}
