using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// xUnit fixture that runs the desktop app's built-in self-verification scenario
/// (<c>MONACO_SELF_VERIFY=1</c>, see <c>MonacoEditorTestApp/MainPage.xaml.cs</c>) and exposes
/// the markers it writes to stdout.
///
/// <para><b>Why not CDP</b>: <see cref="DesktopAppFixture"/> drives the app through Chrome
/// DevTools Protocol, which only Chromium-based WebView2 (Windows) speaks. WebKitGTK (Linux)
/// and WKWebView (macOS) have no CDP endpoint, so stdout is the only channel available there.
/// The app-side scenario drives itself instead: it creates editors across three tab lifecycle
/// stages and probes each one, so a <c>PASS</c> proves the native web view loaded, the editor
/// page navigated, Monaco constructed with a non-zero layout, and the JSON-RPC bridge
/// round-tripped in both directions.</para>
///
/// <para>The <c>SELF_VERIFY_RESULT:</c> line is the authoritative signal. The app can also be
/// asked to exit with a pass/fail code (<c>MONACO_SELF_VERIFY_EXIT=1</c>), but this fixture
/// deliberately does not: terminating from the UI thread can race native window teardown, and
/// this fixture kills the process tree on teardown anyway.</para>
/// </summary>
public sealed class DesktopSelfVerifyFixture : IAsyncLifetime
{
    /// <summary>
    /// Budget for the whole scenario. The app-side readiness wait is 75s per lifecycle stage
    /// across three stages, so this must exceed 225s to distinguish a genuine app-side timeout
    /// (which reports <c>SELF_VERIFY_RESULT:ERROR</c>, a useful diagnostic) from this fixture
    /// giving up (which reports nothing about why).
    /// </summary>
    private const int SelfVerifyTimeoutMs = 300_000;

    /// <summary>Grace period for the final stdout lines to arrive after the process exits.</summary>
    private const int ExitFlushGraceMs = 1_000;

    private const string ResultPrefix = "SELF_VERIFY_RESULT:";
    private const string RuntimeProbePrefix = "SELF_VERIFY_PROBE:";
    private const string FeatureProbePrefix = "SELF_VERIFY_FEATURE_PROBE:";

    private TestAppProcessHost? _host;

    /// <summary>The self-verification outcome: <c>PASS</c>, <c>FAIL</c>, or <c>ERROR:{message}</c>.</summary>
    public string Result { get; private set; } = string.Empty;

    /// <summary>Runtime probe payloads (JSON), one per lifecycle stage, in emission order.</summary>
    public IReadOnlyList<string> RuntimeProbes { get; private set; } = [];

    /// <summary>Feature probe payloads (JSON), one per lifecycle stage, in emission order.</summary>
    public IReadOnlyList<string> FeatureProbes { get; private set; } = [];

    /// <summary>All captured stdout/stderr lines, each prefixed with its channel.</summary>
    public IReadOnlyList<string> LogLines { get; private set; } = [];

    /// <summary>Path of the captured process log under <c>test-artifacts/</c>.</summary>
    public string LogPath { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _host = new TestAppProcessHost(
            "desktop-selfverify",
            new Dictionary<string, string>
            {
                ["MONACO_SELF_VERIFY"] = "1",
                ["MONACO_DIAGNOSTICS"] = "1",
            },
            // Outlive SelfVerifyTimeoutMs so the readers are still attached when a slow run
            // finally reports, rather than silently truncating the log first.
            captureWindow: TimeSpan.FromMilliseconds(SelfVerifyTimeoutMs * 2));

        _host.Start();
        LogPath = _host.LogPath;

        var resultLine = await WaitForResultLineAsync(_host);

        LogLines = _host.GetLinesAfter(0);
        Result = ValueAfter(resultLine, ResultPrefix);
        RuntimeProbes = CollectPayloads(LogLines, RuntimeProbePrefix);
        FeatureProbes = CollectPayloads(LogLines, FeatureProbePrefix);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }
    }

    /// <summary>
    /// Polls the captured log for the result marker. Unlike a plain log wait, this also fails
    /// fast when the app dies before reporting -- the common shape of a missing native web view
    /// runtime -- instead of burning the full timeout on a process that is already gone.
    /// </summary>
    private static async Task<string> WaitForResultLineAsync(TestAppProcessHost host)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(SelfVerifyTimeoutMs);
        var exitObservedAt = default(DateTime?);

        while (DateTime.UtcNow < deadline)
        {
            var line = host.GetLinesAfter(0).FirstOrDefault(l => l.Contains(ResultPrefix, StringComparison.Ordinal));
            if (line is not null)
            {
                return line;
            }

            if (host.HasExited)
            {
                // Give the readers a moment to drain any buffered output written just before exit.
                exitObservedAt ??= DateTime.UtcNow;
                if (DateTime.UtcNow - exitObservedAt.Value > TimeSpan.FromMilliseconds(ExitFlushGraceMs))
                {
                    throw new InvalidOperationException(
                        $"MonacoEditorTestApp exited with code {host.ExitCode} before reporting " +
                        $"'{ResultPrefix}'.\nProcess log:\n{host.CaptureLogSnapshot()}");
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"No '{ResultPrefix}' line appeared within {SelfVerifyTimeoutMs}ms.\n" +
            $"Process log:\n{host.CaptureLogSnapshot()}");
    }

    private static IReadOnlyList<string> CollectPayloads(IReadOnlyList<string> lines, string prefix) =>
        [.. lines.Where(l => l.Contains(prefix, StringComparison.Ordinal)).Select(l => ValueAfter(l, prefix))];

    /// <summary>
    /// Returns the text following <paramref name="prefix"/>, skipping the channel tag the log
    /// capture prepends (<c>[stdout] </c>).
    /// </summary>
    private static string ValueAfter(string line, string prefix)
    {
        var index = line.IndexOf(prefix, StringComparison.Ordinal);
        return index < 0 ? string.Empty : line[(index + prefix.Length)..].Trim();
    }
}
