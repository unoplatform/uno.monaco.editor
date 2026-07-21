using Microsoft.UI.Dispatching;

namespace Monaco.Helpers
{
    /// <summary>
    /// Factory for creating bridge helper instances based on the current platform.
    /// In this task, only WASM variants are created.
    /// Task 5 adds the else branch for desktop variants (using JsonRpc targets).
    /// </summary>
    internal static class BridgeFactory
    {
        /// <summary>
        /// Creates the set of bridge helpers for the given presenter and dispatcher.
        /// </summary>
        /// <returns>A tuple of (IParentAccessor, IThemeListener, IKeyboardListener, IDebugLogger).</returns>
        public static (IParentAccessor ParentAccessor, IThemeListener ThemeListener, IKeyboardListener KeyboardListener, IDebugLogger DebugLogger) Create(
            ICodeEditorPresenter presenter,
            DispatcherQueue queue)
        {
            if (OperatingSystem.IsBrowser())
            {
                var parentAccessor = new ParentAccessor(presenter, queue);
                var themeListener = new ThemeListener(presenter, queue);
                var keyboardListener = new KeyboardListener(presenter, queue);
                var debugLogger = new DebugLogger(presenter);
                return (parentAccessor, themeListener, keyboardListener, debugLogger);
            }
            else
            {
                // Desktop variants will be created here in Task 5
                // using JsonRpc-based bridge classes.
                throw new PlatformNotSupportedException(
                    "Desktop bridge helpers are not yet implemented. See Task 5.");
            }
        }
    }
}
