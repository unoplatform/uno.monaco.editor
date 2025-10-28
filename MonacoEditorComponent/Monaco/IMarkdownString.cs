using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation.Metadata;

#if !NETSTANDARD2_0
using System.Runtime.InteropServices.WindowsRuntime;
#else
using ReadOnlyArray = Monaco.Helpers.Stubs.ReadOnlyArrayAttribute;
#endif

namespace Monaco
{
    /// <summary>
    /// Represents a Markdown string that can be displayed in the editor UI.
    /// </summary>
    public sealed class IMarkdownString(string? svalue, bool isTrusted)
    {
        /// <summary>
        /// Gets or sets a value indicating whether this Markdown content is trusted.
        /// </summary>
        [JsonProperty("isTrusted")]
        public bool IsTrusted { get; set; } = isTrusted;
        
        /// <summary>
        /// Gets or sets a value indicating whether theme icons should be supported in this Markdown.
        /// </summary>
        [JsonProperty("supportThemeIcons", NullValueHandling =NullValueHandling.Ignore)]
        public bool? SupportThemeIcons { get; set; }

        /// <summary>
        /// Gets or sets a mapping of URI strings used in the Markdown.
        /// </summary>
        [JsonProperty("uris", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, Uri>? Uris { get; set; }

        /// <summary>
        /// Gets or sets the Markdown string value.
        /// </summary>
        [JsonProperty("value")]
        public string? Value { get; set; } = svalue;

        public IMarkdownString(string? svalue) : this(svalue, false) { }
    }

    /// <summary>
    /// Extension methods for converting strings to <see cref="IMarkdownString"/>.
    /// </summary>
    public static class MarkdownStringExtensions
    {
        /// <summary>
        /// Converts a string to a Markdown string with default trust settings.
        /// </summary>
        /// <param name="svalue">The string value.</param>
        /// <returns>A new <see cref="IMarkdownString"/> instance.</returns>
        [DefaultOverload]
        public static IMarkdownString ToMarkdownString(this string svalue)
        {
            return ToMarkdownString(svalue, false);
        }

        /// <summary>
        /// Converts a string to a Markdown string with specified trust settings.
        /// </summary>
        /// <param name="svalue">The string value.</param>
        /// <param name="isTrusted">Whether the content is trusted.</param>
        /// <returns>A new <see cref="IMarkdownString"/> instance.</returns>
        [DefaultOverload]
        public static IMarkdownString ToMarkdownString(this string svalue, bool isTrusted)
        {
            return new IMarkdownString(svalue, isTrusted);
        }

        /// <summary>
        /// Converts an array of strings to an array of Markdown strings with default trust settings.
        /// </summary>
        /// <param name="values">The string values.</param>
        /// <returns>An array of <see cref="IMarkdownString"/> instances.</returns>
        public static IMarkdownString[] ToMarkdownString(this string[] values)
        {
            return ToMarkdownString(values, false);
        }

        /// <summary>
        /// Converts an array of strings to an array of Markdown strings with specified trust settings.
        /// </summary>
        /// <param name="values">The string values.</param>
        /// <param name="isTrusted">Whether the content is trusted.</param>
        /// <returns>An array of <see cref="IMarkdownString"/> instances.</returns>
        public static IMarkdownString[] ToMarkdownString(this string[] values, bool isTrusted)
        {
            return [.. values.Select(value => new IMarkdownString(value, isTrusted))];
        }
    }
}
