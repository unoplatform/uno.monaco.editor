namespace Monaco.Helpers;

/// <summary>
/// Pure helper for encoding/decoding strings across the WASM bridge.
/// Extracted from <see cref="ParentAccessor"/> for testability.
/// The WASM bridge uses a custom percent-encoding scheme to safely
/// pass strings containing special characters through JS interop.
/// </summary>
/// <remarks>
/// Two decoders exist because the bridge carries two kinds of payload. <see cref="DecodeTransport"/>
/// undoes the percent-encoding and nothing else, for values that are raw text -- the editor's
/// document and the selected text. <see cref="Desanitize"/> is the long-standing decoder for
/// the JSON-carrying paths (events, actions with parameters, typed property writes) and keeps
/// their historical post-processing.
/// </remarks>
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
    /// Reverses <see cref="Sanitize"/> and nothing else: the result is exactly the string the
    /// JavaScript side encoded.
    /// </summary>
    /// <param name="encoded">The percent-encoded value, or <see langword="null"/>.</param>
    /// <returns>The decoded string, or <see langword="null"/> if the input was <see langword="null"/>.</returns>
    /// <remarks>
    /// This is the decoder for values that are raw text rather than JSON: the document the
    /// content listener reports (<c>Text</c>, <c>ModifiedText</c>) and <c>SelectedText</c>.
    /// Those carry whatever the user typed, so a quotation mark, a backslash, or the two
    /// characters <c>\t</c> in the source are content to preserve, not notation to interpret.
    /// '%' is decoded last to prevent premature unescaping.
    /// </remarks>
    public static string? DecodeTransport(string? encoded)
    {
        if (encoded is null) return null;

        for (var i = 0; i < DesanitizeChars.Length; i++)
        {
            encoded = encoded.Replace($"%{(int)DesanitizeChars[i]}", DesanitizeChars[i].ToString());
        }

        return encoded;
    }

    /// <summary>
    /// Decodes special characters that were encoded by <see cref="Sanitize"/>, then collapses
    /// a <c>\\"</c> run to <c>"</c>. Used by the paths that carry JSON.
    /// </summary>
    /// <remarks>
    /// Not byte-exact because of that final collapse -- for raw text use
    /// <see cref="DecodeTransport"/>.
    /// </remarks>
    public static string? Desanitize(string? parameter)
    {
        var decoded = DecodeTransport(parameter);

        return decoded?.Replace(@"\\""", @"""");
    }
}
