#nullable enable

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
    /// Handles exotic identifiers: <c>$</c>-prefixed names, quoted dotted names,
    /// and other characters that are invalid in C# identifiers.
    /// </summary>
    public static string ToCSharpPropertyName(string tsName)
    {
        var sanitized = SanitizeIdentifier(tsName);
        return PascalCase(sanitized);
    }

    /// <summary>
    /// Returns the original TypeScript wire name for a property, stripping
    /// surrounding quotes if present. Used for <c>[JsonPropertyName]</c> attributes.
    /// </summary>
    public static string GetJsonWireName(string tsName)
    {
        // Strip surrounding single or double quotes from quoted identifiers
        if (tsName.Length >= 2 &&
            ((tsName[0] == '\'' && tsName[^1] == '\'') ||
             (tsName[0] == '"' && tsName[^1] == '"')))
        {
            return tsName[1..^1];
        }

        return tsName;
    }

    /// <summary>
    /// Sanitizes a TypeScript identifier into a valid C# identifier fragment.
    /// Strips <c>$</c> prefixes, surrounding quotes, and replaces dots with underscores.
    /// </summary>
    private static string SanitizeIdentifier(string tsName)
    {
        var name = tsName;

        // Strip surrounding single or double quotes
        if (name.Length >= 2 &&
            ((name[0] == '\'' && name[^1] == '\'') ||
             (name[0] == '"' && name[^1] == '"')))
        {
            name = name[1..^1];
        }

        // Replace dots with underscores (e.g., "semanticHighlighting.enabled")
        if (name.Contains('.'))
        {
            name = string.Join("",
                name.Split('.')
                    .Select(PascalCase));
        }

        // Strip leading $ characters (e.g., "$comment" -> "comment")
        name = name.TrimStart('$');

        // If the name became empty after sanitization, fall back to original
        if (string.IsNullOrEmpty(name))
            name = "Value";

        return name;
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
    /// It is only needed when the JSON wire name differs from the camelCase form of the C# name.
    /// Exotic identifiers (<c>$</c>-prefixed, dotted, quoted) always need the attribute.
    /// </summary>
    public static bool NeedsJsonPropertyName(string tsName, string csharpName)
    {
        var wireName = GetJsonWireName(tsName);
        var camelCSharp = ToCamelCase(csharpName);
        return !string.Equals(wireName, camelCSharp, StringComparison.Ordinal);
    }

    private static string PascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Already starts with uppercase and no underscores -> likely already PascalCase
        return char.ToUpperInvariant(input[0]) + input[1..];
    }
}
