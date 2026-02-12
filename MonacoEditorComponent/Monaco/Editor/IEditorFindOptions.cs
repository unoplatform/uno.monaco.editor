using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// <see href="https://microsoft.github.io/monaco-editor/typedoc/interfaces/editor_editor_api.editor.IEditorFindOptions.html">monaco.editor.IEditorFindOptions</see>
    /// </summary>
    public sealed class IEditorFindOptions
    {
        public bool AutoFindInSelection { get; set; }
        public bool SeedSearchStringFromSelection { get; set; } //= true;
    }
}
