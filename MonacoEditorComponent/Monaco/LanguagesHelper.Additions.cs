using Monaco.Extensions;

namespace Monaco
{

    public sealed partial class LanguagesHelper
    {
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
        /// </summary>
        private static string LanguageIdFromExtensionDesktop(string? extension)
        {
            if (string.IsNullOrEmpty(extension)) return "plaintext";

            // Normalize: strip leading dot, lowercase
            var ext = extension.StartsWith('.') ? extension[1..] : extension;
            ext = ext.ToLowerInvariant();

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
