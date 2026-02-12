using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;

namespace Monaco.Helpers
{
    partial class ThemeListener
    {
        private static readonly ConditionalWeakTable<object, ThemeListener> _instances = [];

        partial void PartialCtor()
        {
            _instances.AddOrUpdate(_owner, this);
        }

        /// <summary>
        /// Removes the registration for the given presenter, allowing safe re-initialization.
        /// </summary>
        internal static void RemoveInstance(ICodeEditorPresenter presenter)
        {
            _instances.Remove(presenter);
        }

        /// <summary>
        /// JSExport entry point: returns the current theme name for the specified presenter owner.
        /// </summary>
        /// <param name="managedOwner">The managed presenter object passed from JavaScript.</param>
        /// <returns>The current theme name string.</returns>
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

        /// <summary>
        /// JSExport entry point: returns whether high contrast mode is active for the specified owner.
        /// </summary>
        /// <param name="managedOwner">The managed presenter object passed from JavaScript.</param>
        /// <returns><see langword="true"/> if high contrast is active.</returns>
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
