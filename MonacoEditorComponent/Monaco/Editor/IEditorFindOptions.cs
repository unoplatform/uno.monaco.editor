using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.ieditorfindoptions.html
    /// </summary>
    public sealed class IEditorFindOptions
    {
        public bool AutoFindInSelection { get; set; }
        public bool SeedSearchStringFromSelection { get; set; } //= true;
    }
}
