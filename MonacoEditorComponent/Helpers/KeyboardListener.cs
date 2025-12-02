using Microsoft.UI.Dispatching;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;

using Windows.Foundation.Metadata;

namespace Monaco.Helpers
{
    public delegate void WebKeyEventHandler(CodeEditor sender, WebKeyEventArgs args);

    public sealed class WebKeyEventArgs
    {
        public int KeyCode { get; set; }

        // TODO: Make these some sort of flagged state enum?
        public bool CtrlKey { get; set; }
        public bool ShiftKey { get; set; }
        public bool AltKey { get; set; }
        public bool MetaKey { get; set; }

        public bool Handled { get; set; }
    }

    [AllowForWeb]
    public sealed partial class KeyboardListener
    {
        private static readonly ConditionalWeakTable<object, KeyboardListener> _instances = [];
        private readonly WeakReference<ICodeEditorPresenter> parent;
        private readonly DispatcherQueue _queue;

        public KeyboardListener(ICodeEditorPresenter parent, DispatcherQueue queue)
        {
            this.parent = new WeakReference<ICodeEditorPresenter>(parent);
            _queue = queue;

            _instances.Add(parent, this);

            PartialCtor(parent);
        }

        partial void PartialCtor(ICodeEditorPresenter parent);

        /// <summary>
        /// Called from JavaScript, returns if event was handled or not.
        /// </summary>
        /// <param name="keycode"></param>
        /// <param name="ctrl"></param>
        /// <param name="shift"></param>
        /// <param name="alt"></param>
        /// <param name="meta"></param>
        /// <returns></returns>
        public bool KeyDown(int keycode, bool ctrl, bool shift, bool alt, bool meta)
        {
            if (parent.TryGetTarget(out var editor))
            {
                return editor.TriggerKeyDown(new WebKeyEventArgs()
                {
                    KeyCode = keycode, // TODO: Convert to a virtual key or something?
                    CtrlKey = ctrl,
                    ShiftKey = shift,
                    AltKey = alt,
                    MetaKey = meta
                });
            }

            return false;
        }

        [JSExport]
        internal static bool NativeKeyDown([JSMarshalAs<JSType.Any>] object managedOwner, int keycode, bool ctrl, bool shift, bool alt, bool meta)
        {
            if (_instances.TryGetValue(managedOwner, out var listener))
            {
                return listener.KeyDown(keycode, ctrl, shift, alt, meta);
            }
            else
            {
                throw new InvalidOperationException($"KeyboardListener not found for owner {managedOwner}");
            }
        }
    }
}
