using Monaco;
using System.Text.Json.Serialization;

namespace Monaco.Languages
{
    public interface WorkspaceFileEdit
    {
        WorkspaceEditMetadata Metadata { get; set; }

        Uri NewUri { get; set; }

        Uri OldUri { get; set; }

        WorkspaceFileEditOptions Options { get; set; }
    }
}
