using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// <see href="https://microsoft.github.io/monaco-editor/typedoc/interfaces/editor_editor_api.editor.IMarker.html">monaco.editor.IMarker</see>
    /// </summary>
    public interface IMarker : IMarkerData
    {
        string? Owner { get; set; }

        Uri? Resource { get; set; }
    }
}
