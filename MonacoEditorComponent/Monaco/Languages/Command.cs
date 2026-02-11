using System.Text.Json.Serialization;

namespace Monaco.Languages
{
    /// <summary>
    /// A command that should be run upon acceptance of this item.
    /// </summary>
    public sealed class Command
    {
        public object[]? Arguments { get; set; }

        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Tooltip { get; set; }
    }
}
