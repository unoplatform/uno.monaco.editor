using Microsoft.UI.Dispatching;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;

using Windows.Foundation.Metadata;

namespace Monaco.Helpers
{
    /// <summary>
    /// Delegate for handling keyboard events from the Monaco editor.
    /// </summary>
    /// <param name="sender">The <see cref="CodeEditor"/> that raised the event.</param>
    /// <param name="args">The key event arguments.</param>
    public delegate void WebKeyEventHandler(CodeEditor sender, WebKeyEventArgs args);

    /// <summary>
    /// Provides data for keyboard events raised by the Monaco editor.
    /// </summary>
    public sealed class WebKeyEventArgs
    {
        /// <summary>Gets or sets the JavaScript key code of the pressed key.</summary>
        public int KeyCode { get; set; }

        /// <summary>Gets or sets a value indicating whether the Ctrl key was held.</summary>
        public bool CtrlKey { get; set; }

        /// <summary>Gets or sets a value indicating whether the Shift key was held.</summary>
        public bool ShiftKey { get; set; }

        /// <summary>Gets or sets a value indicating whether the Alt key was held.</summary>
        public bool AltKey { get; set; }

        /// <summary>Gets or sets a value indicating whether the Meta (Cmd/Win) key was held.</summary>
        public bool MetaKey { get; set; }

        /// <summary>Gets or sets a value indicating whether the event has been handled.</summary>
        public bool Handled { get; set; }
    }

    /// <summary>
    /// Listens for keyboard events from the Monaco editor and routes them to the parent
    /// <see cref="CodeEditor"/>. On WASM, events arrive via JSExport; on desktop, via JSON-RPC.
    /// </summary>
    [AllowForWeb]
    public sealed partial class KeyboardListener : IKeyboardListener
    {
        private static readonly ConditionalWeakTable<object, KeyboardListener> _instances = [];
        private readonly WeakReference<ICodeEditorPresenter> parent;
        private readonly DispatcherQueue _queue;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyboardListener"/> class.
        /// </summary>
        /// <param name="parent">The presenter that owns this listener.</param>
        /// <param name="queue">The UI thread dispatcher.</param>
        public KeyboardListener(ICodeEditorPresenter parent, DispatcherQueue queue)
        {
            this.parent = new WeakReference<ICodeEditorPresenter>(parent);
            _queue = queue;

            _instances.AddOrUpdate(parent, this);

            PartialCtor(parent);
        }

        /// <summary>
        /// Removes the registration for the given presenter, allowing safe re-initialization.
        /// </summary>
        internal static void RemoveInstance(ICodeEditorPresenter presenter)
        {
            _instances.Remove(presenter);
        }

        partial void PartialCtor(ICodeEditorPresenter parent);

        /// <summary>
        /// Called from JavaScript, returns if event was handled or not.
        /// </summary>
        /// <param name="keycode">The JavaScript key code of the pressed key.</param>
        /// <param name="ctrl"><see langword="true"/> if the Ctrl modifier was held.</param>
        /// <param name="shift"><see langword="true"/> if the Shift modifier was held.</param>
        /// <param name="alt"><see langword="true"/> if the Alt modifier was held.</param>
        /// <param name="meta"><see langword="true"/> if the Meta (Win/Cmd) modifier was held.</param>
        /// <returns><see langword="true"/> if the editor handled the key event; otherwise, <see langword="false"/>.</returns>
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
            if (!OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException("NativeKeyDown is only available on WASM. Desktop uses JSON-RPC keyboard/keyDown.");
            }

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
