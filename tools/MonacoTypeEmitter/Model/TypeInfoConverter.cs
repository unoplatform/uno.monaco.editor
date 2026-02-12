#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonacoTypeEmitter.Model;

/// <summary>
/// Custom converter that deserializes TypeInfo as a flat object with all
/// fields present (using the "kind" discriminator to determine semantics).
/// </summary>
public sealed class TypeInfoConverter : JsonConverter<TypeInfo>
{
    public override TypeInfo? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var element = doc.RootElement;

        var result = new TypeInfo();

        if (element.TryGetProperty("kind", out var kindProp))
            result.Kind = kindProp.GetString() ?? "intrinsic";

        if (element.TryGetProperty("name", out var nameProp))
            result.Name = nameProp.GetString();

        if (element.TryGetProperty("text", out var textProp))
            result.Text = textProp.GetString();

        if (element.TryGetProperty("operator", out var opProp))
            result.Operator = opProp.GetString();

        if (element.TryGetProperty("value", out var valueProp))
        {
            result.Value = valueProp.ValueKind switch
            {
                JsonValueKind.String => valueProp.GetString(),
                JsonValueKind.Number => valueProp.TryGetInt64(out var l) ? l : valueProp.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        if (element.TryGetProperty("typeArguments", out var typeArgsProp))
            result.TypeArguments = Deserialize<List<TypeInfo>>(typeArgsProp, options);

        if (element.TryGetProperty("types", out var typesProp))
            result.Types = Deserialize<List<TypeInfo>>(typesProp, options);

        if (element.TryGetProperty("elementType", out var elemProp))
            result.ElementType = Deserialize<TypeInfo>(elemProp, options);

        if (element.TryGetProperty("elementTypes", out var elemsProp))
            result.ElementTypes = Deserialize<List<TypeInfo>>(elemsProp, options);

        if (element.TryGetProperty("parameters", out var paramsProp))
            result.Parameters = Deserialize<List<ParameterInfo>>(paramsProp, options);

        if (element.TryGetProperty("returnType", out var retProp))
            result.ReturnType = Deserialize<TypeInfo>(retProp, options);

        if (element.TryGetProperty("typeParameters", out var tpProp))
            result.TypeParameters = Deserialize<List<TypeParameterInfo>>(tpProp, options);

        if (element.TryGetProperty("properties", out var propsProp))
            result.Properties = Deserialize<List<PropertyInfo>>(propsProp, options);

        if (element.TryGetProperty("methods", out var methProp))
            result.Methods = Deserialize<List<MethodInfo>>(methProp, options);

        if (element.TryGetProperty("indexSignatures", out var idxProp))
            result.IndexSignatures = Deserialize<List<IndexSignatureInfo>>(idxProp, options);

        if (element.TryGetProperty("callSignatures", out var callProp))
            result.CallSignatures = Deserialize<List<CallSignatureInfo>>(callProp, options);

        if (element.TryGetProperty("objectType", out var objProp))
            result.ObjectType = Deserialize<TypeInfo>(objProp, options);

        if (element.TryGetProperty("indexType", out var idxTypeProp))
            result.IndexType = Deserialize<TypeInfo>(idxTypeProp, options);

        if (element.TryGetProperty("type", out var typeProp))
            result.Type = Deserialize<TypeInfo>(typeProp, options);

        return result;
    }

    public override void Write(Utf8JsonWriter writer, TypeInfo value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("TypeInfo serialization is not needed by the emitter.");
    }

    private static T? Deserialize<T>(JsonElement element, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<T>(element.GetRawText(), options);
    }
}
