namespace Monaco.Helpers
{
    /// <summary>
    /// Interface for keyboard listener implementations.
    /// WASM uses the concrete KeyboardListener with JSExport.
    /// Desktop will use a JsonRpc-based variant (Task 5).
    /// </summary>
    internal interface IKeyboardListener
    {
        /// <summary>
        /// Called from JavaScript, returns if the event was handled or not.
        /// </summary>
        bool KeyDown(int keycode, bool ctrl, bool shift, bool alt, bool meta);
    }
}
