using StreamJsonRpc;

namespace Monaco.Helpers;

/// <summary>
/// Desktop implementation of <see cref="IKeyboardListener"/> that receives
/// keyboard/keyDown JSON-RPC notifications from JavaScript and routes them
/// to <see cref="ICodeEditorPresenter.TriggerKeyDown(WebKeyEventArgs)"/>.
/// </summary>
internal sealed class KeyboardListenerDesktop : IKeyboardListener
{
    private readonly WeakReference<ICodeEditorPresenter> _parent;

    public KeyboardListenerDesktop(ICodeEditorPresenter parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        _parent = new WeakReference<ICodeEditorPresenter>(parent);
    }

    public bool KeyDown(int keycode, bool ctrl, bool shift, bool alt, bool meta)
    {
        if (_parent.TryGetTarget(out var editor))
        {
            return editor.TriggerKeyDown(new WebKeyEventArgs
            {
                KeyCode = keycode,
                CtrlKey = ctrl,
                ShiftKey = shift,
                AltKey = alt,
                MetaKey = meta,
            });
        }

        return false;
    }

    // ============================================================
    // JSON-RPC target method
    // ============================================================

    [JsonRpcMethod("keyboard/keyDown")]
    public void OnKeyDown(int keyCode, bool ctrlKey, bool shiftKey, bool altKey, bool metaKey)
    {
        KeyDown(keyCode, ctrlKey, shiftKey, altKey, metaKey);
    }
}
