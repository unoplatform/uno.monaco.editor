using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    public sealed class IModelDecorationMinimapOptions
    {
        /// <summary>
        /// CSS color to render.
        /// e.g.: rgba(100, 100, 100, 0.5) or a color from the color registry
        /// </summary>
        public string? Color { get; set; }

        /// <summary>
        /// CSS color to render.
        /// e.g.: rgba(100, 100, 100, 0.5) or a color from the color registry
        /// </summary>
        public string? DarkColor { get; set; }

        /// <summary>
        /// The position in the overview ruler.
        /// </summary>
        public int Position { get; set; }
    }
}