using Monaco;
using Monaco.Editor;
using System.Text.Json.Serialization;

namespace Monaco.Languages
{
    /// <summary>
    /// Contains additional diagnostic information about the context in which
    /// a [code action](#CodeActionProvider.provideCodeActions) is run.
    /// </summary>
    public sealed class CodeActionContext
    {
        /// <summary>
        /// An array of diagnostics.
        /// </summary>
        public MarkerData[]? Markers { get; set; } // TODO: Should setup the serialization mappings between interfaces to leave interfaces here...

        /// <summary>
        /// Requested kind of actions to return.
        /// </summary>
        public string? Only { get; set; }
    }
}

