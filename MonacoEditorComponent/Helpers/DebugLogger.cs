using System.Diagnostics;

using Windows.Foundation.Metadata;

namespace Monaco.Helpers
{
    /// <summary>
    /// Provides debug-only logging for the Monaco editor bridge layer. Messages are
    /// written to <see cref="System.Diagnostics.Debug"/> output in DEBUG builds only.
    /// </summary>
    [AllowForWeb]
    public sealed partial class DebugLogger : IDebugLogger
    {
        /// <inheritdoc />
#pragma warning disable CA1822 // Mark members as static
        public void Log(string message)
#pragma warning restore CA1822 // Mark members as static
        {
#if DEBUG
            Debug.WriteLine(message);
#endif
        }
    }
}
