using System.Text.Json.Serialization;

namespace Monaco.Languages
{
    public sealed class ILanguageExtensionPoint
    {
        public string[]? Aliases { get; set; }
        public Uri? Configuration { get; set; }
        public string[]? Extensions { get; set; }
        public string[]? FilenamePatterns { get; set; }
        public string[]? Filenames { get; set; }
        public string? FirstLine { get; set; }
        public string? Id { get; set; }
        public string[]? Mimetypes { get; set; }
    }
}
