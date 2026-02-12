using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// <see href="https://microsoft.github.io/monaco-editor/typedoc/interfaces/editor_editor_api.editor.IWordAtPosition.html">monaco.editor.IWordAtPosition</see>
    /// </summary>
    public sealed class WordAtPosition : IWordAtPosition
    {
        /// <summary>
        /// Column where the word ends.
        /// </summary>
        [JsonInclude]
        public uint EndColumn { get; internal set; }

        /// <summary>
        /// Column where the word starts.
        /// </summary>
        [JsonInclude]
        public uint StartColumn { get; internal set; }

        /// <summary>
        /// The word.
        /// </summary>
        [JsonInclude]
        public string? Word { get; internal set; }
    }
}
