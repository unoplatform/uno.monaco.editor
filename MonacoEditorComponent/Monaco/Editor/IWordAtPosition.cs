using System.Text.Json.Serialization;
using Monaco.Helpers;

namespace Monaco.Editor
{
    /// <summary>
    /// https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.iwordatposition.html
    /// </summary>
    [JsonConverter(typeof(InterfaceToClassConverter<IWordAtPosition, WordAtPosition>))]
    public interface IWordAtPosition
    {
        /// <summary>
        /// Column where the word ends.
        /// </summary>
        uint EndColumn { get; }

        /// <summary>
        /// Column where the word starts.
        /// </summary>
        uint StartColumn { get; }

        /// <summary>
        /// The word.
        /// </summary>
        string? Word { get; }
    }
}
