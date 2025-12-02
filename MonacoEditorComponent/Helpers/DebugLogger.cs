using System.Diagnostics;

using Windows.Foundation.Metadata;

namespace Monaco.Helpers
{
    [AllowForWeb]
    public sealed partial class DebugLogger
    {
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
