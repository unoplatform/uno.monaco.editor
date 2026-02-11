using System;
using System.Text.Json;
using System.Text.Json.Serialization;

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

}
