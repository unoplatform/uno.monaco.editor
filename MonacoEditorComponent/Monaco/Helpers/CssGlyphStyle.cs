using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace Monaco.Helpers
{
    /// <summary>
    /// Represents a CSS style for a glyph decoration in the editor's glyph margin.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(CssGlyphStyleConverter))]
    [Newtonsoft.Json.JsonConverter(typeof(NewtonsoftCssStyleConverter))]
    public sealed class CssGlyphStyle : ICssStyle
    {
        /// <summary>
        /// Gets or sets the URI of the image to display as the glyph.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public System.Uri? GlyphImage { get; set; }

        /// <summary>
        /// Gets the unique identifier for this CSS style.
        /// </summary>
        public uint Id { get; }

        /// <summary>
        /// Gets the CSS class name for this style.
        /// </summary>
        public string? Name { get; }

        public CssGlyphStyle()
        {
            Id = CssStyleBroker.Register(this);
            Name = "generated-style-" + Id;
        }

        /// <summary>
        /// Converts this glyph style to a CSS string.
        /// </summary>
        /// <returns>A CSS string representation of this glyph style.</returns>
        public string ToCss()
        {
            return this.WrapCssClassName($"background: url(\"{GlyphImage?.AbsoluteUriString()}\");");
        }
    }
}
