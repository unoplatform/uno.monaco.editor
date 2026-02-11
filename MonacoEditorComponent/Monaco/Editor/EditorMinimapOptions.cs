using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Control the behavior and rendering of the minimap.
    ///
    /// Configuration options for editor minimap
    /// </summary>
    public sealed class EditorMinimapOptions
    {
        /// <summary>
        /// Enable the rendering of the minimap.
        /// Defaults to true.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Limit the width of the minimap to render at most a certain number of columns.
        /// Defaults to 120.
        /// </summary>
        public uint? MaxColumn { get; set; }

        /// <summary>
        /// Render the actual text on a line (as opposed to color blocks).
        /// Defaults to true.
        /// </summary>
        public bool? RenderCharacters { get; set; }

        /// <summary>
        /// Relative size of the font in the minimap. Defaults to 1.
        /// </summary>
        public double? Scale { get; set; }

        /// <summary>
        /// Control the rendering of the minimap slider.
        /// Defaults to 'mouseover'.
        /// </summary>
        public Show? ShowSlider { get; set; }

        /// <summary>
        /// Control the side of the minimap in editor.
        /// Defaults to 'right'.
        /// </summary>
        public Side? Side { get; set; }
    }
}
