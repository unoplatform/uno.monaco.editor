using System.IO;

using Monaco.Extensions;

namespace Monaco
{

    public sealed partial class LanguagesHelper
    {
        /// <summary>
        /// Returns the Monaco language identifier for the given file extension, filename, or path.
        /// </summary>
        /// <param name="extension">
        /// A bare extension (e.g. <c>"cs"</c>), dotted extension (<c>".cs"</c>),
        /// filename (<c>"Program.cs"</c>), or full path (<c>"src/Program.cs"</c>).
        /// </param>
        /// <returns>
        /// The Monaco language identifier (e.g. <c>"csharp"</c>), or <c>"plaintext"</c>
        /// when no mapping exists.
        /// </returns>
        /// <remarks>
        /// On WASM this delegates to the Monaco JavaScript runtime via JSImport.
        /// On desktop a C#-side lookup table provides an equivalent mapping.
        /// </remarks>
#pragma warning disable CA1822 // Mark members as static
        public string GetCodeLanguageFromExtension(string extension)
        {
            if (OperatingSystem.IsBrowser())
            {
                return NativeMethods.LanguageIdFromExtension(extension);
            }

            // Desktop fallback: C#-side extension-to-language mapping.
            // Covers the most common Monaco language identifiers.
            return LanguageIdFromExtensionDesktop(extension);
        }
#pragma warning restore CA1822 // Mark members as static

        /// <summary>
        /// C#-side mapping from file extension to Monaco language identifier.
        /// Used on desktop where JSImport is not available.
        /// Accepts bare extensions ("cs", ".cs"), filenames ("test.cs"),
        /// and full paths ("src/foo/test.cs") for parity with JS behavior.
        /// </summary>
        internal static string LanguageIdFromExtensionDesktop(string? extension)
        {
            if (string.IsNullOrEmpty(extension)) return "plaintext";

            // If input looks like a filename or path, extract just the extension.
            // Path.GetExtension handles ".cs", "test.cs", "foo/bar.cs" uniformly.
            var extracted = Path.GetExtension(extension);
            string ext;
            if (!string.IsNullOrEmpty(extracted))
            {
                // Path.GetExtension returns ".cs" -- strip the leading dot.
                ext = extracted[1..].ToLowerInvariant();
            }
            else
            {
                // No extension extracted -- handle bare dotted extensions like ".cs"
                // (Path.GetExtension(".cs") returns empty) and bare names like "cs".
                ext = extension.TrimStart('.').ToLowerInvariant();
            }

            return ext switch
            {
                "bat" or "cmd" => "bat",
                "c" or "h" => "c",
                "clj" or "cljs" or "cljc" => "clojure",
                "coffee" => "coffeescript",
                "cpp" or "cc" or "cxx" or "hpp" or "hh" or "hxx" => "cpp",
                "cs" or "csx" => "csharp",
                "css" => "css",
                "dart" => "dart",
                // Registered by the component rather than by Monaco -- see
                // ts-helpermethods/languages/diff.ts.
                "diff" or "patch" => "diff",
                "dockerfile" => "dockerfile",
                "fs" or "fsi" or "fsx" => "fsharp",
                "go" => "go",
                "graphql" or "gql" => "graphql",
                "handlebars" or "hbs" => "handlebars",
                "htm" or "html" or "xhtml" => "html",
                "ini" or "cfg" => "ini",
                "java" => "java",
                "js" or "mjs" or "cjs" => "javascript",
                "json" or "jsonc" => "json",
                "jsx" => "javascript",
                "kt" or "kts" => "kotlin",
                "less" => "less",
                "lua" => "lua",
                "md" or "markdown" => "markdown",
                "m" or "mm" => "objective-c",
                "pas" or "pp" => "pascal",
                "php" => "php",
                "pl" or "pm" => "perl",
                "ps1" or "psm1" or "psd1" => "powershell",
                "py" or "pyw" => "python",
                "r" or "rmd" => "r",
                "razor" or "cshtml" => "razor",
                "rb" or "erb" => "ruby",
                "rs" => "rust",
                "scala" or "sc" => "scala",
                "scss" => "scss",
                "sh" or "bash" or "zsh" => "shell",
                "sql" => "sql",
                "swift" => "swift",
                "ts" => "typescript",
                "tsx" => "typescript",
                "vb" => "vb",
                "xml" or "xsd" or "xsl" or "xslt" or "svg" or "csproj" or "fsproj" or "props" or "targets" or "sln" or "slnx" => "xml",
                "yaml" or "yml" => "yaml",
                _ => "plaintext",
            };
        }
    }
}
