using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    public interface IStandaloneEditorConstructionOptions : IEditorConstructionOptions, IGlobalEditorOptions
    {
        /// <summary>
        /// The initial model associated with this code editor.
        /// </summary>
        IModel? Model { get; set; }
        /// <summary>
        /// The initial value of the auto created model in the editor.
        /// To not create automatically a model, use `model: null`.
        /// </summary>
        string? Value { get; set; }
        /// <summary>
        /// The initial language of the auto created model in the editor.
        /// To not create automatically a model, use `model: null`.
        /// </summary>
        string? Language { get; set; }
        /// <summary>
        /// Initial theme to be used for rendering.
        /// The current out-of-the-box available themes are: 'vs' (default), 'vs-dark', 'hc-black'.
        /// You can create custom themes via `monaco.editor.defineTheme`.
        /// To switch a theme, use `monaco.editor.setTheme`
        /// </summary>
        string? Theme { get; set; }
        /// <summary>
        /// An URL to open when Ctrl+H (Windows and Linux) or Cmd+H (OSX) is pressed in
        /// the accessibility help dialog in the editor.
        ///
        /// Defaults to "https://go.microsoft.com/fwlink/?linkid=852450"
        /// </summary>
        string? AccessibilityHelpUrl { get; set; }
    }
}
