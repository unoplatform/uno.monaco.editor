using Monaco.Editor;
using System.Text.Json.Serialization;

namespace Monaco.Languages
{
    /// <summary>
    /// String representations for a color.
    /// <seealso href="https://microsoft.github.io/monaco-editor/typedoc/interfaces/editor_editor_api.languages.IColorPresentation.html">monaco.languages.IColorPresentation</seealso>
    /// </summary>
    public sealed class ColorPresentation(string label)
    {
        /// <summary>
        /// An optional array of additional text edits that are applied when
        /// selecting this completion. Edits must not overlap with the main edit
        /// nor with themselves.
        /// </summary>
        public ISingleEditOperation[]? AdditionalTextEdits { get; set; }

        /// <summary>
        /// The label of this color presentation. It will be shown on the color picker header. 
        /// By default this is also the text that is inserted when selecting this color presentation.
        /// </summary>
        public string? Label { get; set; } = label;

        public ISingleEditOperation? TextEdit { get; set; }
    }
}
