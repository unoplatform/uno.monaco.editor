using Monaco.Helpers;
using Newtonsoft.Json;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.UI;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Monaco.Languages
{
    /// <summary>
    /// A color range is a range in a text model which represents a color.
    /// </summary>
    public sealed class ColorInformation(Color color, IRange? range)
    {
        [JsonPropertyName("color")]
        [JsonProperty("color")]
        [System.Text.Json.Serialization.JsonConverter(typeof(ColorConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(NewtonsoftColorConverter))]
        public Color Color { get; set; } = color;

        [JsonPropertyName("range")]
        [JsonProperty("range")]
        [System.Text.Json.Serialization.JsonConverter(typeof(InterfaceToClassConverter<IRange, Range>))]
        [Newtonsoft.Json.JsonConverter(typeof(NewtonsoftInterfaceToClassConverter<IRange, Range>))]
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
                        color.A = (byte)(reader.GetDouble() * 255);
                        break;
                    case "red":
                        color.R = (byte)(reader.GetDouble() * 255);
                        break;
                    case "green":
                        color.G = (byte)(reader.GetDouble() * 255);
                        break;
                    case "blue":
                        color.B = (byte)(reader.GetDouble() * 255);
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
    }

    /// <summary>
    /// Newtonsoft converter between <see cref="Windows.UI.Color"/> and Monaco IColor.
    /// Retained for dual-stack compatibility until Newtonsoft is removed.
    /// </summary>
    internal class NewtonsoftColorConverter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Color) || t == typeof(Color?);

        public override object? ReadJson(JsonReader reader, Type t, object? existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            Color color = new();

            if (reader.Read())
            {
                while (reader.TokenType != JsonToken.EndObject)
                {
                    switch (reader.Value)
                    {
                        case "alpha":
                            color.A = (byte)((reader.ReadAsDouble() ?? 0) * 255);
                            break;
                        case "red":
                            color.R = (byte)((reader.ReadAsDouble() ?? 0) * 255);
                            break;
                        case "green":
                            color.G = (byte)((reader.ReadAsDouble() ?? 0) * 255);
                            break;
                        case "blue":
                            color.B = (byte)((reader.ReadAsDouble() ?? 0) * 255);
                            break;
                    }

                    reader.Read(); // Advance past Number Token read above to next property
                }
            }

            return color;
        }

        public override void WriteJson(JsonWriter writer, object? untypedValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (Color)untypedValue;

            writer.WriteStartObject();
            writer.WritePropertyName("alpha");
            writer.WriteValue(value.A / 255F);
            writer.WritePropertyName("red");
            writer.WriteValue(value.R / 255F);
            writer.WritePropertyName("green");
            writer.WriteValue(value.G / 255F);
            writer.WritePropertyName("blue");
            writer.WriteValue(value.B / 255F);
            writer.WriteEndObject();
        }
    }
}
