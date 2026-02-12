using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Configure the editor's hover.
    ///
    /// Configuration options for editor hover
    /// </summary>
    public sealed class EditorHoverOptions
    {
        /// <summary>
        /// Delay for showing the hover.
        /// Defaults to 300.
        /// </summary>
        public int? Delay { get; set; }

        /// <summary>
        /// Enable the hover.
        /// Defaults to true.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Is the hover sticky such that it can be clicked and its contents selected?
        /// Defaults to true.
        /// </summary>
        public bool? Sticky { get; set; }
    }

}
