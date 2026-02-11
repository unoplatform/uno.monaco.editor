using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.imarkerdata.html
    /// </summary>
    public sealed class MarkerData : IMarkerData
    {
        public string? Code { get; set; }

        public uint EndColumn { get; set; }

        public uint EndLineNumber { get; set; }

        public string? Message { get; set; }

        public IRelatedInformation[]? RelatedInformation { get; set; }

        public MarkerSeverity Severity { get; set; }

        public string? Source { get; set; }

        public uint StartColumn { get; set; }

        public uint StartLineNumber { get; set; }

        public MarkerTag[]? Tags { get; set; }

        public MarkerData() { }

        /// <summary>
        /// Initializes a new <see cref="MarkerData"/> instance in the specified <see cref="Range"/>. Provided as a helper.
        /// </summary>
        /// <param name="range"><see cref="Range"/> to scope Marker on.</param>
        public MarkerData(Range range)
        {
            StartLineNumber = range.StartLineNumber;
            StartColumn = range.StartColumn;
            EndLineNumber = range.EndLineNumber;
            EndColumn = range.EndColumn;
        }
    }
}
