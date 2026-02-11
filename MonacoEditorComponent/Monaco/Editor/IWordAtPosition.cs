using Newtonsoft.Json;
using System.Text.Json.Serialization;
using Monaco.Helpers;

namespace Monaco.Editor
{
    /// <summary>
    /// https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.iwordatposition.html
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(InterfaceToClassConverter<IWordAtPosition, WordAtPosition>))]
    [Newtonsoft.Json.JsonConverter(typeof(NewtonsoftInterfaceToClassConverter<IWordAtPosition, WordAtPosition>))]
    public interface IWordAtPosition
    {
        /// <summary>
        /// Column where the word ends.
        /// </summary>
        [JsonProperty("endColumn")]
        uint EndColumn { get; }

        /// <summary>
        /// Column where the word starts.
        /// </summary>
        [JsonProperty("startColumn")]
        uint StartColumn { get; }

        /// <summary>
        /// The word.
        /// </summary>
        [JsonProperty("word")]
        string? Word { get; }
    }
}
