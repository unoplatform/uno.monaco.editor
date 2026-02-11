namespace Monaco.Helpers
{
    /// <summary>
    /// Interface for debug logger implementations.
    /// WASM uses the concrete DebugLogger with JSExport.
    /// Desktop will use a JsonRpc-based variant (Task 5).
    /// </summary>
    internal interface IDebugLogger
    {
        /// <summary>
        /// Logs a debug message.
        /// </summary>
        void Log(string message);
    }
}
