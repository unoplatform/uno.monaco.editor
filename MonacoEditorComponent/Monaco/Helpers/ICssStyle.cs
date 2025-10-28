using Newtonsoft.Json;
using System;
using System.Linq;

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

    internal class CssStyleConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(ICssStyle).IsAssignableFrom(objectType);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) => new NotSupportedException();

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is ICssStyle style)
            {
                writer.WriteValue(style.Name);
            }
        }
    }
}
