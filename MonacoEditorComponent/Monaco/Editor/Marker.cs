using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.imarker.html
    /// </summary>
    public sealed class Marker : IMarker
    {
        public string? Code { get; set; }

        public uint EndColumn { get; set; }

        public uint EndLineNumber { get; set; }

        public string? Message { get; set; }

        public string? Owner { get; set; }

        public IRelatedInformation[]? RelatedInformation { get; set; }

        public Uri? Resource { get; set; }

        public MarkerSeverity Severity { get; set; }

        public string? Source { get; set; }

        public uint StartColumn { get; set; }

        public uint StartLineNumber { get; set; }

        public MarkerTag[]? Tags { get; set; }
    }
}
