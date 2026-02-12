using System.Text.Json.Serialization;

namespace Monaco.Languages
{
    public interface WorkspaceFileEditOptions
    {
        bool Copy { get; set; }

        bool Folder { get; set; }

        bool IgnoreIfExists { get; set; }

        bool IgnoreIfNotExists { get; set; }

        double MaxSize { get; set; }

        bool Overwrite { get; set; }

        bool Recursive { get; set; }

        bool SkipTrashBin { get; set; }
    }
}