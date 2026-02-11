using System.Text.Json.Serialization;

namespace Monaco.Languages
{
    public interface WorkspaceEditMetadata
    {
        string Description { get; set; }

        string Label { get; set; }

        bool NeedsConfirmation { get; set; }
    }
}