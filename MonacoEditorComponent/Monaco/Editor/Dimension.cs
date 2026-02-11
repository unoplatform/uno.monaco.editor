using Monaco.Editor;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// The initial editor dimension (to avoid measuring the container).
    /// </summary>
    public sealed class Dimension : IDimension
    {
        public uint Height { get; set; }

        public uint Width { get; set; }
    }
}
