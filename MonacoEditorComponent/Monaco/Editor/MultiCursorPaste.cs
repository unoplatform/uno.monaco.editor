using Newtonsoft.Json;
using System;

namespace Monaco.Editor
{

    /// <summary>
    /// Configure the behaviour when pasting a text with the line count equal to the cursor
    /// count.
    /// Defaults to 'spread'.
    /// </summary>
    [JsonConverter(typeof(MultiCursorPasteConverter))]
    public enum MultiCursorPaste { Full, Spread };

    internal class MultiCursorPasteConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(MultiCursorPaste) || t == typeof(MultiCursorPaste?);

        public override object? ReadJson(JsonReader reader, Type t, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            return value switch
            {
                "full" => MultiCursorPaste.Full,
                "spread" => MultiCursorPaste.Spread,
                _ => throw new Exception("Cannot unmarshal type MultiCursorPaste"),
            };
        }

        public override void WriteJson(JsonWriter writer, object? untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (MultiCursorPaste)untypedValue;
            switch (value)
            {
                case MultiCursorPaste.Full:
                    serializer.Serialize(writer, "full");
                    return;
                case MultiCursorPaste.Spread:
                    serializer.Serialize(writer, "spread");
                    return;
            }
            throw new Exception("Cannot marshal type MultiCursorPaste");
        }
    }
}
