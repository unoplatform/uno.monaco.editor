using Newtonsoft.Json;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Monaco.Helpers
{
    /// <summary>
    /// STJ converter: deserializes an interface as its concrete class.
    /// </summary>
    /// <typeparam name="TInterface">Type of base Interface.</typeparam>
    /// <typeparam name="TClass">Type of class to use for deserializing object with interface.</typeparam>
    internal class InterfaceToClassConverter<TInterface, TClass> : System.Text.Json.Serialization.JsonConverter<TInterface>
        where TClass : TInterface
    {
        public override TInterface? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<TClass>(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, typeof(TClass), options);
        }
    }

    /// <summary>
    /// Newtonsoft converter: deserializes an interface as its concrete class.
    /// Retained for dual-stack compatibility until Newtonsoft is removed.
    /// </summary>
    /// <typeparam name="TInterface">Type of base Interface.</typeparam>
    /// <typeparam name="TClass">Type of class to use for deserializing object with interface.</typeparam>
    internal class NewtonsoftInterfaceToClassConverter<TInterface, TClass> : Newtonsoft.Json.JsonConverter where TClass : TInterface, new()
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TInterface);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            var pop = new TClass();
            serializer.Populate(reader, pop);
            return pop;
        }

        public override void WriteJson(JsonWriter writer, object? value, Newtonsoft.Json.JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
