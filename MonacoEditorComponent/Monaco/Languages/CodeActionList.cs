using Monaco;
using Monaco.Editor;

namespace Monaco.Languages
{
    /// <summary>
    /// Represents a list of code actions provided by a code action provider.
    /// </summary>
    public sealed class CodeActionList // IDisposable??
    {
        /// <summary>
        /// Gets or sets the array of code actions.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("actions", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CodeAction[]? Actions { get; set; }

    }
}

