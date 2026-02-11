
#if !NETSTANDARD2_0
#else
using ReadOnlyArrayAttribute = Monaco.Helpers.Stubs.ReadOnlyArrayAttribute;
using System.Text.Json.Serialization;
#endif

namespace Monaco.Languages
{
    /// <summary>
    /// A hover represents additional information for a symbol or word. Hovers are
    /// rendered in a tooltip-like widget.
    /// </summary>
    public sealed class Hover(string[] contents, IRange range, bool isTrusted)
    {
        /// <summary>
        /// The contents of this hover.
        /// </summary>
        public IMarkdownString[] Contents { get; set; } = contents.ToMarkdownString(isTrusted);

        /// <summary>
        /// The range to which this hover applies. When missing, the
        /// editor will use the range at the current position or the
        /// current position itself.
        /// </summary>
        public IRange Range { get; set; } = range;

        public Hover(string[] contents, IRange range) : this(contents, range, false) { }
    }
}
