using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using Uno;
using Uno.Foundation;

namespace Monaco.Helpers
{
	partial class ThemeListener
	{
        private static ConditionalWeakTable<object, ThemeListener> _instances = new();
        
        partial void PartialCtor()
		{
            _instances.Add(_owner, this);
		}

        [Preserve]
        public static string NativeGetCurrentThemeName([JSMarshalAs<JSType.Any>] object managedOwner)
        {
            if (_instances.TryGetValue(managedOwner, out var logger))
            {
                return logger.CurrentThemeName;
            }
            else
            {
                throw new InvalidOperationException($"ThemeListener not found for owner {managedOwner}");
            }
        }

        [Preserve]
        public static bool NativeGetIsHighContrast([JSMarshalAs<JSType.Any>] object managedOwner)
        {
            if (_instances.TryGetValue(managedOwner, out var logger))
            {
                return logger.IsHighContrast;
            }
            else
            {
                throw new InvalidOperationException($"ThemeListener not found for owner {managedOwner}");
            }
        }
    }
}
