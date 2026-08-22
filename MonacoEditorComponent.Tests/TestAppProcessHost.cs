using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Launches <c>MonacoEditorTestApp</c> as a desktop process and captures its stdout/stderr
/// into both a timestamped log file under <c>test-artifacts/</c> and an in-memory buffer
/// exposed through a cursor-based query API.
///
/// <para>Transport-agnostic on purpose: it knows nothing about CDP, Playwright or WebView2.
/// <see cref="DesktopAppFixture"/> layers Chromium CDP on top of it for the Windows-only
/// integration tests; <see cref="DesktopSelfVerifyFixture"/> uses it on its own to read the
/// self-verification markers the app writes to stdout, which is the only channel available
/// on WebKitGTK (Linux) and WKWebView (macOS).</para>
/// </summary>
internal sealed class TestAppProcessHost : IAsyncDisposable
{
    /// <summary>
    /// Upper bound on how long the background readers stay attached to the process streams.
    /// Configurable because it must outlive the consumer's own readiness budget: the CDP
    /// fixture is ready within a minute, while the self-verify scenario can legitimately run
    /// for several (75s readiness x 3 lifecycle stages, app-side).
    /// </summary>
    private static readonly TimeSpan DefaultCaptureWindow = TimeSpan.FromMinutes(5);

    private readonly string _logFilePrefix;
    private readonly IReadOnlyDictionary<string, string> _environment;
    private readonly TimeSpan _captureWindow;

    // In-memory log lines for the cursor-based query API.
    private readonly List<string> _logLines = [];
    private readonly object _logLock = new();

    private Process? _process;
    private CancellationTokenSource? _logCaptureCts;

    /// <param name="logFilePrefix">Base name for the captured log, e.g. <c>desktop-fixture</c>.</param>
    /// <param name="environment">Environment variables applied to the launched process.</param>
    /// <param name="captureWindow">How long to keep reading the process streams. Defaults to 5 minutes.</param>
    public TestAppProcessHost(
        string logFilePrefix,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan? captureWindow = null)
    {
        _logFilePrefix = logFilePrefix;
        _environment = environment;
        _captureWindow = captureWindow ?? DefaultCaptureWindow;
    }

    /// <summary>Path of the captured process log. Empty until <see cref="Start"/> is called.</summary>
    public string LogPath { get; private set; } = string.Empty;

    /// <summary>True once the process has been started and has since exited.</summary>
    public bool HasExited => _process is { HasExited: true };

    /// <summary>Exit code of the launched process, or null while it is still running.</summary>
    public int? ExitCode => _process is { HasExited: true } exited ? exited.ExitCode : null;

    /// <summary>
    /// Starts the pre-built desktop app. <c>-c Release --no-build</c> mirrors what CI produces,
    /// so this never triggers a Debug rebuild; <c>--no-launch-profile</c> keeps
    /// <c>launchSettings.json</c> environment variables from interfering with the caller's.
    /// </summary>
    public void Start()
    {
        var repoRoot = FindRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "test-artifacts");
        Directory.CreateDirectory(artifactsDir);
        LogPath = Path.Combine(artifactsDir, $"{_logFilePrefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");

        var testAppProject = Path.Combine(repoRoot, "MonacoEditorTestApp", "MonacoEditorTestApp.csproj");
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments =
                $"run --project \"{testAppProject}\" -f net10.0-desktop -c Release --no-build --no-launch-profile",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var (key, value) in _environment)
        {
            startInfo.Environment[key] = value;
        }

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start MonacoEditorTestApp desktop process.");

        _logCaptureCts = new CancellationTokenSource();
        _ = CaptureProcessOutputAsync(_process, LogPath, _logCaptureCts.Token);
    }

    /// <summary>
    /// Waits for the process to exit. Returns false on timeout, which the caller decides how
    /// to treat -- a GUI app that was not asked to self-terminate never exits on its own.
    /// </summary>
    public async Task<bool> WaitForExitAsync(TimeSpan timeout)
    {
        if (_process is null)
        {
            return false;
        }

        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await _process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    // ============================================================
    // Cursor-based log query API
    // ============================================================

    /// <summary>
    /// Returns a cursor (index) representing the current end of the log buffer.
    /// Lines added after this cursor are "new" from the caller's perspective.
    /// </summary>
    public int GetLogCursor()
    {
        lock (_logLock)
        {
            return _logLines.Count;
        }
    }

    /// <summary>
    /// Waits for a log line matching <paramref name="pattern"/> (regex) to appear
    /// after the given <paramref name="cursor"/> position. Returns the matching line.
    /// </summary>
    /// <param name="cursor">The cursor position returned by <see cref="GetLogCursor"/>.</param>
    /// <param name="pattern">A regex pattern to match against log lines.</param>
    /// <param name="timeoutMs">Maximum time to wait in milliseconds.</param>
    /// <returns>The first matching log line after the cursor.</returns>
    /// <exception cref="TimeoutException">Thrown if no matching line appears within the timeout.</exception>
    public async Task<string> WaitForLogLineAfterAsync(int cursor, string pattern, int timeoutMs = 10_000)
    {
        var regex = new Regex(pattern, RegexOptions.Compiled);
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            lock (_logLock)
            {
                for (int i = cursor; i < _logLines.Count; i++)
                {
                    if (regex.IsMatch(_logLines[i]))
                    {
                        return _logLines[i];
                    }
                }
            }

            await Task.Delay(50);
        }

        // Build diagnostic message with available lines.
        var availableLines = GetLinesAfter(cursor);
        throw new TimeoutException(
            $"No log line matching '{pattern}' appeared within {timeoutMs}ms after cursor {cursor}.\n" +
            $"Lines after cursor ({availableLines.Count}):\n" +
            string.Join("\n", availableLines.Take(50)));
    }

    /// <summary>
    /// Returns all log lines captured after the given <paramref name="cursor"/> position.
    /// </summary>
    /// <param name="cursor">The cursor position returned by <see cref="GetLogCursor"/>.</param>
    /// <returns>A list of log lines after the cursor.</returns>
    public List<string> GetLinesAfter(int cursor)
    {
        lock (_logLock)
        {
            if (cursor >= _logLines.Count) return [];
            return _logLines.GetRange(cursor, _logLines.Count - cursor);
        }
    }

    /// <summary>
    /// Returns the tail of the captured log, suitable for embedding in an exception message.
    /// </summary>
    public string CaptureLogSnapshot()
    {
        // Keep only the most recent lines: the tail is the relevant diagnostic near a
        // failure, and the full buffer can make exceptions unreadably large.
        const int maxLines = 200;
        var lines = GetLinesAfter(0);
        if (lines.Count == 0)
        {
            return "(no log)";
        }
        if (lines.Count <= maxLines)
        {
            return string.Join(Environment.NewLine, lines);
        }

        var omitted = lines.Count - maxLines;
        var tail = lines.GetRange(lines.Count - maxLines, maxLines);
        return $"(... {omitted} earlier line(s) omitted; showing last {maxLines} ...)" +
            Environment.NewLine + string.Join(Environment.NewLine, tail);
    }

    /// <summary>
    /// Builds an artifact path sharing the timestamped base name of the process log
    /// (e.g. <c>desktop-fixture-{timestamp}-cdp-version.txt</c>) so a run's diagnostics group
    /// together and don't overwrite each other across parallel fixture instances or retries.
    /// Returns null when the log directory cannot be determined.
    /// </summary>
    public string? ArtifactSiblingPath(string suffix)
    {
        var dir = Path.GetDirectoryName(LogPath);
        if (string.IsNullOrEmpty(dir))
        {
            return null;
        }
        return Path.Join(dir, $"{Path.GetFileNameWithoutExtension(LogPath)}-{suffix}");
    }

    // ============================================================
    // Teardown
    // ============================================================

    /// <summary>
    /// Stops log capture and kills the process tree. Separated from <see cref="DisposeAsync"/>
    /// so callers that own additional resources can control ordering -- the CDP fixture must
    /// kill the app before closing the Playwright browser, or the close can hang.
    /// </summary>
    public async Task ShutdownAsync()
    {
        // Cancel log capture first so stream reads don't block process disposal.
        if (_logCaptureCts is not null)
        {
            try { await _logCaptureCts.CancelAsync(); } catch { /* best-effort */ }
            _logCaptureCts.Dispose();
            _logCaptureCts = null;
        }

        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync(new CancellationTokenSource(5000).Token);
            }
            catch { /* best-effort cleanup */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync();
        _process?.Dispose();
        _process = null;
    }

    // ============================================================
    // Helpers
    // ============================================================

    /// <summary>
    /// Walks up from the test assembly location to the repository root (the directory
    /// containing <c>.git</c>).
    /// </summary>
    public static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }

        // Fallback: navigate up from BaseDirectory assuming standard build output layout.
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private async Task CaptureProcessOutputAsync(Process process, string logPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var logWriter = new StreamWriter(logPath, append: false);
            // Use a SemaphoreSlim to serialize StreamWriter access from concurrent
            // stdout/stderr readers. _logLock guards List<string> but StreamWriter
            // is not thread-safe and needs its own synchronization.
            using var writerSemaphore = new SemaphoreSlim(1, 1);

            var stdoutTask = CaptureStreamAsync(
                process.StandardOutput, "stdout", logWriter, writerSemaphore, cancellationToken);
            var stderrTask = CaptureStreamAsync(
                process.StandardError, "stderr", logWriter, writerSemaphore, cancellationToken);

            await Task.WhenAny(Task.WhenAll(stdoutTask, stderrTask), Task.Delay(_captureWindow, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Expected when the host is being disposed -- log capture is no longer needed.
        }
        catch
        {
            // Best-effort log capture -- never throw from background task.
        }
    }

    private Task CaptureStreamAsync(
        StreamReader reader,
        string channel,
        StreamWriter logWriter,
        SemaphoreSlim writerSemaphore,
        CancellationToken cancellationToken)
        // ReadLineAsync returns null at end-of-stream, avoiding the CA2024
        // diagnostic from synchronous EndOfStream checks in async methods.
        => Task.Run(async () =>
        {
            string? line;
            while (!cancellationToken.IsCancellationRequested &&
                   (line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                var formattedLine = $"[{channel}] {line}";
                lock (_logLock)
                {
                    _logLines.Add(formattedLine);
                }
                await writerSemaphore.WaitAsync(cancellationToken);
                try
                {
                    await logWriter.WriteLineAsync(formattedLine.AsMemory(), cancellationToken);
                    await logWriter.FlushAsync(cancellationToken);
                }
                finally
                {
                    writerSemaphore.Release();
                }
            }
        }, cancellationToken);
}
