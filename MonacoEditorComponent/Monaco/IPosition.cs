using System.Text.Json.Serialization;

namespace Monaco
{
    /// <summary>
    /// A position in the editor. This interface is suitable for serialization.
    /// </summary>
    public interface IPosition
    {
        /// <summary>
        /// column (the first character in a line is between column 1 and column 2)
        /// </summary>
        uint Column { get; }

        /// <summary>
        /// line number (starts at 1)
        /// </summary>
        uint LineNumber { get; }
    }
}
