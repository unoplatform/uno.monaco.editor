namespace Monaco.Helpers;

/// <summary>
/// Pure helper for encoding/decoding strings across the WASM bridge.
/// Extracted from <see cref="ParentAccessor"/> for testability.
/// The WASM bridge uses a custom percent-encoding scheme to safely
/// pass strings containing special characters through JS interop.
/// </summary>
internal static class BridgeEncoding
{
    /// <summary>
    /// Characters that are encoded by Sanitize. The order matters for Desanitize:
    /// '%' must be decoded LAST to prevent double-decoding of escape sequences.
    /// Note: '%' is listed first in the Sanitize replacement string so it is
    /// encoded first (preventing double-encoding of other replacements).
    /// </summary>
    private static readonly string SanitizeChars = @"%&\""'{}:,";

    /// <summary>
    /// Characters that are decoded by Desanitize. '%' is decoded last
    /// to prevent premature unescaping of percent-encoded sequences.
    /// </summary>
    private static readonly string DesanitizeChars = @"&\""'{}:,%";

    /// <summary>
    /// Encodes special characters in a JSON string for safe transport through the WASM bridge.
    /// Each character in the replacement set is replaced with %{charCode}.
    /// '%' is encoded first to prevent double-encoding.
    /// </summary>
    public static string? Sanitize(string? jsonString)
    {
        if (jsonString is null) return null;

        for (var i = 0; i < SanitizeChars.Length; i++)
        {
            jsonString = jsonString.Replace(SanitizeChars[i].ToString(), "%" + (int)SanitizeChars[i]);
        }

        return jsonString;
    }

    /// <summary>
    /// Decodes special characters that were encoded by <see cref="Sanitize"/>.
    /// '%' is decoded last to prevent premature unescaping.
    /// </summary>
    public static string? Desanitize(string? parameter)
    {
        if (parameter is null) return parameter;

        for (var i = 0; i < DesanitizeChars.Length; i++)
        {
            parameter = parameter.Replace($"%{(int)DesanitizeChars[i]}", DesanitizeChars[i].ToString());
        }

        parameter = parameter.Replace(@"\\""", @"""");

        return parameter;
    }
}
