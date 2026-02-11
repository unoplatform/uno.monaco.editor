using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// A structure defining a problem/warning/etc.
    /// </summary>
    public interface IMarkerData : IRange
    {
        string? Code { get; set; }

        string? Message { get; set; }

        IRelatedInformation[]? RelatedInformation { get; set; }

        MarkerSeverity Severity { get; set; }

        string? Source { get; set; }

        MarkerTag[]? Tags { get; set; }
    }
}
