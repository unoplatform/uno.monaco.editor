using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// The initial editor dimension (to avoid measuring the container).
    /// </summary>
    public interface IDimension
    {
        uint Height { get; set; }

        uint Width { get; set; }
    }
}
