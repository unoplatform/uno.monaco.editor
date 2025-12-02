using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;

namespace Monaco.Helpers
{
    partial class DebugLogger
    {
        private static readonly ConditionalWeakTable<object, DebugLogger> _instances = [];

        public DebugLogger(ICodeEditorPresenter codeEditor)
        {
            _instances.Add(codeEditor, this);

            Log("created");
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
