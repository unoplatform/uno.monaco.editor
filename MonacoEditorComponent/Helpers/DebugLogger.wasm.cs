using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;

namespace Monaco.Helpers
{
    partial class DebugLogger
    {
        private static readonly ConditionalWeakTable<object, DebugLogger> _instances = [];

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
