using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    public sealed class IEditorScrollbarOptions
    {
        /// <summary>
        /// Always consume mouse wheel events (always call preventDefault() and stopPropagation() on
        /// the browser events).
        /// Defaults to true.
        /// </summary>
        public bool? AlwaysConsumeMouseWheel { get; set; }

        /// <summary>
        /// The size of arrows (if displayed).
        /// Defaults to 11.
        /// </summary>
        public int? ArrowSize { get; set; }

        /// <summary>
        /// Listen to mouse wheel events and react to them by scrolling.
        /// Defaults to true.
        /// </summary>
        public bool? HandleMouseWheel { get; set; }

        /// <summary>
        /// Render horizontal scrollbar.
        /// Defaults to 'auto'.
        /// </summary>
        public ScrollbarBehavior? Horizontal { get; set; }

        /// <summary>
        /// Render arrows at the left and right of the horizontal scrollbar.
        /// Defaults to false.
        /// </summary>
        public bool? HorizontalHasArrows { get; set; }

        /// <summary>
        /// Height in pixels for the horizontal scrollbar.
        /// Defaults to 10 (px).
        /// </summary>
        public int? HorizontalScrollbarSize { get; set; }

        /// <summary>
        /// Height in pixels for the horizontal slider.
        /// Defaults to `horizontalScrollbarSize`.
        /// </summary>
        public int? HorizontalSliderSize { get; set; }

        /// <summary>
        /// Cast horizontal and vertical shadows when the content is scrolled.
        /// Defaults to true.
        /// </summary>
        public bool? UseShadows { get; set; }

        /// <summary>
        /// Render vertical scrollbar.
        /// Defaults to 'auto'.
        /// </summary>
        public ScrollbarBehavior? Vertical { get; set; }

        /// <summary>
        /// Render arrows at the top and bottom of the vertical scrollbar.
        /// Defaults to false.
        /// </summary>
        public bool? VerticalHasArrows { get; set; }

        /// <summary>
        /// Width in pixels for the vertical scrollbar.
        /// Defaults to 10 (px).
        /// </summary>
        public int? VerticalScrollbarSize { get; set; }

        /// <summary>
        /// Width in pixels for the vertical slider.
        /// Defaults to `verticalScrollbarSize`.
        /// </summary>
        public int? VerticalSliderSize { get; set; }
    }

}
