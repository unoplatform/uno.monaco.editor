using System.Diagnostics;

using StreamJsonRpc;

namespace Monaco.Helpers;

/// <summary>
/// Desktop implementation of <see cref="IDebugLogger"/> that receives
/// debug/log JSON-RPC notifications from JavaScript and routes them
/// to <see cref="Debug.WriteLine(string)"/>.
/// </summary>
internal sealed class DebugLoggerDesktop : IDebugLogger
{
    public void Log(string message)
    {
#if DEBUG
        Debug.WriteLine($"[JS] {message}");
#endif
    }

    // ============================================================
    // JSON-RPC target method
    // ============================================================

    [JsonRpcMethod("debug/log")]
    public void OnLog(string level, string message)
    {
        Log($"[{level}] {message}");
    }
}
