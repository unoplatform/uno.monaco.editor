using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;

namespace Monaco.Helpers
{
    partial class DebugLogger
    {
        private static readonly ConditionalWeakTable<object, DebugLogger> _instances = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugLogger"/> class and registers
        /// it against the specified presenter for JSExport dispatch.
        /// </summary>
        /// <param name="codeEditor">The presenter that owns this logger.</param>
        public DebugLogger(ICodeEditorPresenter codeEditor)
        {
            _instances.AddOrUpdate(codeEditor, this);

            Log("created");
        }

        /// <summary>
        /// Removes the registration for the given presenter, allowing safe re-initialization.
        /// </summary>
        internal static void RemoveInstance(ICodeEditorPresenter presenter)
        {
            _instances.Remove(presenter);
        }

        /// <summary>
        /// JSExport entry point: logs a debug message for the specified presenter owner.
        /// </summary>
        /// <param name="managedOwner">The managed presenter object passed from JavaScript.</param>
        /// <param name="message">The message to log.</param>
        [JSExport]
        public static void NativeLog([JSMarshalAs<JSType.Any>] object managedOwner, string message)
        {
            if (_instances.TryGetValue(managedOwner, out var logger))
            {
                logger.Log(message);
            }
            else
            {
                throw new InvalidOperationException($"DebugLogger not found for owner {managedOwner}");
            }
        }
    }
}
