using Monaco.Helpers;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.UI;

namespace Monaco.Languages
{
    /// <summary>
    /// A color range is a range in a text model which represents a color.
    /// </summary>
    public sealed class ColorInformation(Color color, IRange? range)
    {
        [JsonPropertyName("color")]
        [System.Text.Json.Serialization.JsonConverter(typeof(ColorConverter))]
        public Color Color { get; set; } = color;

        [JsonPropertyName("range")]
        [System.Text.Json.Serialization.JsonConverter(typeof(InterfaceToClassConverter<IRange, Range>))]
        public IRange? Range { get; set; } = range;
    }

    /// <summary>
    /// STJ converter between <see cref="Windows.UI.Color"/> and Monaco IColor (0-1 float RGBA).
    /// </summary>
    internal class ColorConverter : System.Text.Json.Serialization.JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new System.Text.Json.JsonException("Expected StartObject token for Color.");
            }

            Color color = new();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new System.Text.Json.JsonException("Expected PropertyName token.");
                }

                var propertyName = reader.GetString();
                reader.Read(); // Advance to the value

                switch (propertyName)
                {
                    case "alpha":
                        color.A = ClampToByte(reader.GetDouble());
                        break;
                    case "red":
                        color.R = ClampToByte(reader.GetDouble());
                        break;
                    case "green":
                        color.G = ClampToByte(reader.GetDouble());
                        break;
                    case "blue":
                        color.B = ClampToByte(reader.GetDouble());
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return color;
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("alpha", value.A / 255F);
            writer.WriteNumber("red", value.R / 255F);
            writer.WriteNumber("green", value.G / 255F);
            writer.WriteNumber("blue", value.B / 255F);
            writer.WriteEndObject();
        }

        /// <summary>
        /// Clamps a 0-1 float channel value and converts to a 0-255 byte.
        /// </summary>
        private static byte ClampToByte(double value)
        {
            return (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
        }
    }

}
