using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monaco.Helpers
{
    /// <summary>
    /// Represents a CSS style that can be applied to editor decorations.
    /// </summary>
    public interface ICssStyle
    {
        /// <summary>
        /// Gets the unique identifier for this CSS style.
        /// </summary>
        uint Id { get; }

        /// <summary>
        /// Gets the CSS class name for this style.
        /// </summary>
        string? Name { get; }

        /// <summary>
        /// Converts this style to a CSS string.
        /// </summary>
        /// <returns>A CSS string representation of this style.</returns>
        string ToCss();
    }

    /// <summary>
    /// Extension methods for <see cref="ICssStyle"/>.
    /// </summary>
    public static class ICssStyleExtensions
    {
        /// <summary>
        /// Wraps CSS rules with the style's class name selector.
        /// </summary>
        /// <param name="style">The CSS style.</param>
        /// <param name="inner">The inner CSS rules.</param>
        /// <returns>A complete CSS rule with class selector.</returns>
        public static string WrapCssClassName(this ICssStyle style, string inner)
        {
            return string.Format(".{0} {{ {1} }}", style.Name, inner);
        }
    }

    /// <summary>
    /// STJ write-only converter for <see cref="CssLineStyle"/>.
    /// Serializes as the style's <see cref="ICssStyle.Name"/> string.
    /// </summary>
    internal class CssLineStyleConverter : System.Text.Json.Serialization.JsonConverter<CssLineStyle>
    {
        public override CssLineStyle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("CssStyleConverter is write-only.");
        }

        public override void Write(Utf8JsonWriter writer, CssLineStyle value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Name);
        }
    }

    /// <summary>
    /// STJ write-only converter for <see cref="CssGlyphStyle"/>.
    /// Serializes as the style's <see cref="ICssStyle.Name"/> string.
    /// </summary>
    internal class CssGlyphStyleConverter : System.Text.Json.Serialization.JsonConverter<CssGlyphStyle>
    {
        public override CssGlyphStyle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("CssStyleConverter is write-only.");
        }

        public override void Write(Utf8JsonWriter writer, CssGlyphStyle value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Name);
        }
    }

    /// <summary>
    /// STJ write-only converter for <see cref="CssInlineStyle"/>.
    /// Serializes as the style's <see cref="ICssStyle.Name"/> string.
    /// </summary>
    internal class CssInlineStyleConverter : System.Text.Json.Serialization.JsonConverter<CssInlineStyle>
    {
        public override CssInlineStyle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("CssStyleConverter is write-only.");
        }

        public override void Write(Utf8JsonWriter writer, CssInlineStyle value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Name);
        }
    }

}
