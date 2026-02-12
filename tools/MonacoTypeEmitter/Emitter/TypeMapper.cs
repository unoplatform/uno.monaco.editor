#nullable enable

using System.Text.Json;
using MonacoTypeEmitter.Model;

namespace MonacoTypeEmitter.Emitter;

/// <summary>
/// Maps TypeScript type information to C# type strings.
/// </summary>
public static class TypeMapper
{
    /// <summary>
    /// Converts a TypeInfo to a C# type string.
    /// </summary>
    public static string ToCSharpType(TypeInfo typeInfo, bool forceNullable = false)
    {
        var result = ToCSharpTypeCore(typeInfo);
        if (forceNullable && !result.EndsWith("?"))
            result += "?";
        return result;
    }

    private static string ToCSharpTypeCore(TypeInfo typeInfo)
    {
        return typeInfo.Kind switch
        {
            "primitive" => MapPrimitive(typeInfo.Name ?? "object"),
            "reference" => MapReference(typeInfo),
            "array" => MapArray(typeInfo),
            "union" => MapUnion(typeInfo),
            "intersection" => MapIntersection(typeInfo),
            "literal" => MapLiteral(typeInfo),
            "function" => "object", // Functions map to object in the existing patterns
            "objectLiteral" => "object", // Inline object literals map to object
            "tuple" => MapTuple(typeInfo),
            "indexedAccess" => "object", // Complex indexed access maps to object
            "typeOperator" => MapTypeOperator(typeInfo),
            "conditional" => "object", // Conditional types map to object
            "intrinsic" => typeInfo.Text ?? "object",
            _ => "object"
        };
    }

    private static string MapPrimitive(string name)
    {
        return name switch
        {
            "string" => "string",
            "number" => "double",
            "boolean" => "bool",
            "void" => "void",
            "null" => "object",
            "undefined" => "object",
            "any" => "object",
            "unknown" => "object",
            "never" => "object",
            "bigint" => "long",
            "symbol" => "object",
            "object" => "object",
            "this" => "object",
            _ => "object"
        };
    }

    private static string MapReference(TypeInfo typeInfo)
    {
        var name = typeInfo.Name ?? "object";

        // Map well-known TS types to C# equivalents
        var mapped = name switch
        {
            "Promise" or "PromiseLike" or "Thenable" =>
                typeInfo.TypeArguments is { Count: > 0 }
                    ? $"object" // Promise<T> -> object in existing patterns
                    : "object",
            "Record" => "object",
            "Map" => "object",
            "Set" => "object",
            "RegExp" => "string",
            "Uri" => "Uri",
            "Uint8Array" => "byte[]",
            "Uint32Array" => "uint[]",
            "Int32Array" => "int[]",
            "Float32Array" => "float[]",
            "Float64Array" => "double[]",
            "ArrayBuffer" => "byte[]",
            "HTMLElement" or "HTMLDivElement" => "object",
            "Event" => "object",
            "CSSStyleDeclaration" => "object",
            _ => name
        };

        // Preserve type arguments for non-collapsed reference types
        if (typeInfo.TypeArguments is { Count: > 0 } && mapped == name)
        {
            var typeArgs = string.Join(", ", typeInfo.TypeArguments.Select(ta => ToCSharpTypeCore(ta)));
            return $"{mapped}<{typeArgs}>";
        }

        return mapped;
    }

    private static string MapArray(TypeInfo typeInfo)
    {
        if (typeInfo.ElementType is null)
            return "object[]";

        var elementType = ToCSharpTypeCore(typeInfo.ElementType);
        return $"{elementType}[]";
    }

    private static string MapUnion(TypeInfo typeInfo)
    {
        if (typeInfo.Types is null || typeInfo.Types.Count == 0)
            return "object";

        // Filter out null/undefined from union
        var nonNullTypes = typeInfo.Types
            .Where(t => !(t.Kind == "primitive" && (t.Name == "null" || t.Name == "undefined")))
            .ToList();

        if (nonNullTypes.Count == 0)
            return "object";

        if (nonNullTypes.Count == 1)
            return ToCSharpTypeCore(nonNullTypes[0]);

        // Check if it's a string literal union (maps to string enum or just string)
        if (nonNullTypes.All(t => t.Kind == "literal" && t.Value is string))
            return "string";

        // Multiple non-null types -> use object
        return "object";
    }

    private static string MapIntersection(TypeInfo typeInfo)
    {
        // Intersections generally map to object in C#
        return "object";
    }

    private static string MapLiteral(TypeInfo typeInfo)
    {
        if (typeInfo.Value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => "string",
                JsonValueKind.Number => "double",
                JsonValueKind.True or JsonValueKind.False => "bool",
                _ => "object"
            };
        }

        return typeInfo.Value switch
        {
            string => "string",
            long or int or double => "double",
            bool => "bool",
            _ => "object"
        };
    }

    private static string MapTuple(TypeInfo typeInfo)
    {
        // Tuples map to object[] in existing patterns
        return "object[]";
    }

    private static string MapTypeOperator(TypeInfo typeInfo)
    {
        return typeInfo.Operator switch
        {
            "readonly" when typeInfo.Type is not null => ToCSharpTypeCore(typeInfo.Type),
            "keyof" => "string",
            _ => "object"
        };
    }

    /// <summary>
    /// Determines if a C# type is a value type (needs ? for nullable).
    /// </summary>
    public static bool IsValueType(string csharpType)
    {
        return csharpType is "int" or "uint" or "long" or "ulong"
            or "short" or "ushort" or "byte" or "sbyte"
            or "float" or "double" or "decimal"
            or "bool" or "char";
    }

    /// <summary>
    /// Determines if a C# type is an enum type based on known Monaco enum names.
    /// </summary>
    public static bool IsEnumType(string csharpType, HashSet<string> knownEnumNames)
    {
        // Strip nullable suffix
        var typeName = csharpType.TrimEnd('?');
        return knownEnumNames.Contains(typeName);
    }
}
