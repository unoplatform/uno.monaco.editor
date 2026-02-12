#nullable enable

using System.Globalization;
using System.Text;

namespace MonacoTypeEmitter.Emitter;

/// <summary>
/// Maps TypeScript/Monaco namespace and type names to C# equivalents.
/// </summary>
public static class NameMapper
{
    /// <summary>
    /// Converts a Monaco namespace (e.g., "monaco.editor") to a C# namespace (e.g., "Monaco.Editor").
    /// </summary>
    public static string ToCSharpNamespace(string monacoNamespace)
    {
        var parts = monacoNamespace.Split('.');
        return string.Join('.', parts.Select(PascalCase));
    }

    /// <summary>
    /// Converts a Monaco namespace to a relative directory path for file output.
    /// "monaco" -> "" (root), "monaco.editor" -> "Editor", "monaco.languages" -> "Languages".
    /// </summary>
    public static string ToRelativeDirectory(string monacoNamespace)
    {
        var parts = monacoNamespace.Split('.');
        if (parts.Length <= 1)
            return ""; // Root "monaco" namespace maps to Monaco/ root

        return string.Join(Path.DirectorySeparatorChar.ToString(),
            parts.Skip(1).Select(PascalCase));
    }

    /// <summary>
    /// Converts a TypeScript property name to a C# PascalCase property name.
    /// </summary>
    public static string ToCSharpPropertyName(string tsName)
    {
        return PascalCase(tsName);
    }

    /// <summary>
    /// Converts a TypeScript enum member name to PascalCase for C#.
    /// Handles SCREAMING_SNAKE_CASE, kebab-case, and camelCase.
    /// </summary>
    public static string ToCSharpEnumMemberName(string tsName)
    {
        // If it contains underscores or dashes, split and pascal-case each part
        if (tsName.Contains('_') || tsName.Contains('-'))
        {
            return string.Join("",
                tsName.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => PascalCase(part.ToLowerInvariant())));
        }

        return PascalCase(tsName);
    }

    /// <summary>
    /// Returns the camelCase form of a PascalCase C# name, for JsonPropertyName comparison.
    /// </summary>
    public static string ToCamelCase(string pascalName)
    {
        if (string.IsNullOrEmpty(pascalName))
            return pascalName;

        if (char.IsLower(pascalName[0]))
            return pascalName;

        return char.ToLowerInvariant(pascalName[0]) + pascalName[1..];
    }

    /// <summary>
    /// Determines whether a [JsonPropertyName] attribute is needed.
    /// It is only needed when the JSON name differs from the CamelCase form of the C# name.
    /// </summary>
    public static bool NeedsJsonPropertyName(string tsName, string csharpName)
    {
        var camelCSharp = ToCamelCase(csharpName);
        return !string.Equals(tsName, camelCSharp, StringComparison.Ordinal);
    }

    private static string PascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Already starts with uppercase and no underscores -> likely already PascalCase
        return char.ToUpperInvariant(input[0]) + input[1..];
    }
}
