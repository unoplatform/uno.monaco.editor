using System.Text.Json.Serialization;

namespace Monaco.Editor
{

    /// <summary>
    /// Control indentation of wrapped lines. Can be: 'none', 'same', 'indent' or 'deepIndent'.
    /// Defaults to 'same' in vscode and to 'none' in monaco-editor.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<WrappingIndent>))]
    public enum WrappingIndent
    {
        [JsonStringEnumMemberName("deepIndent")]
        DeepIndent,
        [JsonStringEnumMemberName("indent")]
        Indent,
        [JsonStringEnumMemberName("none")]
        None,
        [JsonStringEnumMemberName("same")]
        Same,
    };
}
