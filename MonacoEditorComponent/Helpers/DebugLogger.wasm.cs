using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;

namespace Monaco.Helpers
{
	partial class DebugLogger
	{
        private static ConditionalWeakTable<object, DebugLogger> _instances = new();

        public DebugLogger(CodeEditor codeEditor)
		{
            _instances.Add(codeEditor, this);

            Log("created");
		}

        [JSExport]
        public static void NativeLog([JSMarshalAs<JSType.Any>] object managedOwner, string message)
        {
            if(_instances.TryGetValue(managedOwner, out var logger))
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
