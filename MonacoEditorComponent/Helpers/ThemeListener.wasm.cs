using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;

namespace Monaco.Helpers
{
    partial class ThemeListener
    {
        private static readonly ConditionalWeakTable<object, ThemeListener> _instances = [];

        partial void PartialCtor()
        {
            _instances.Add(_owner, this);
        }

        [JSExport]
        public static string ManagedGetCurrentThemeName([JSMarshalAs<JSType.Any>] object managedOwner)
        {
            if (_instances.TryGetValue(managedOwner, out var listener))
            {
                return listener.CurrentThemeName;
            }
            else
            {
                throw new InvalidOperationException($"ThemeListener not found for owner {managedOwner}");
            }
        }

        [JSExport]
        public static bool ManagedGetIsHighContrast([JSMarshalAs<JSType.Any>] object managedOwner)
        {
            if (_instances.TryGetValue(managedOwner, out var listener))
            {
                return listener.IsHighContrast;
            }
            else
            {
                throw new InvalidOperationException($"ThemeListener not found for owner {managedOwner}");
            }
        }
    }
}
