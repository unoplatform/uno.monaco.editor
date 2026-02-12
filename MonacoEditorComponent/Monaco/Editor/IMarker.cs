using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.imarker.html
    /// </summary>
    public interface IMarker : IMarkerData
    {
        string? Owner { get; set; }

        Uri? Resource { get; set; }
    }
}
