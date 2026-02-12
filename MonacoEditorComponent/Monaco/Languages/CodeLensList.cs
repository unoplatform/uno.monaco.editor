using System.Text.Json.Serialization;
namespace Monaco.Languages
{

    /// <summary>
    /// Represents a list of code lenses provided by a code lens provider.
    /// </summary>
    public sealed class CodeLensList // IDisposible?
    {
        /// <summary>
        /// Gets or sets the array of code lenses.
        /// </summary>
        public CodeLens[]? Lenses { get; set; }
    }
}

