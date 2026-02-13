using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

using Microsoft.Playwright;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// xUnit fixture that launches MonacoEditorTestApp as a desktop process with
/// Chrome DevTools Protocol (CDP) enabled on WebView2, then connects Playwright
/// via <see cref="IBrowserType.ConnectOverCDPAsync"/>.
///
/// <para><b>Windows only</b>: WebView2 on Windows is Chromium-based and supports CDP.
/// macOS (WKWebView) and Linux (WebKitGTK) are not Chromium and do not support CDP.</para>
///
/// <para><b>Deterministic readiness</b>: The fixture does NOT rely on arbitrary delays.
/// It polls <c>http://localhost:{port}/json/version</c> to confirm CDP is ready,
/// then waits for the Monaco editor page via Playwright page enumeration.</para>
///
/// <para><b>Agent-driven testing pattern (Playwright MCP)</b>:
/// An AI agent can also connect to a running desktop app for ad-hoc verification
/// using the Playwright MCP server with <c>--cdp-endpoint http://localhost:{port}</c>.
/// This provides <c>browser_snapshot</c> for accessibility tree inspection,
/// <c>browser_evaluate</c> for JS assertions, and <c>browser_click</c> for
/// interaction testing. This is a development convenience, not automated CI.</para>
/// </summary>
public sealed class DesktopAppFixture : IAsyncLifetime
{
    private const int CdpPollIntervalMs = 500;
    private const int CdpReadyTimeoutMs = 30_000;
    private const int MonacoPageTimeoutMs = 10_000;
    // CI cold-start (Windows runner) can take significantly longer than local dev:
    // dotnet run launches the pre-built app, WebView2 initializes, then Monaco loads.
    // 15s was insufficient on CI run 21957402273; 60s provides adequate headroom.
    private const int MonacoReadyTimeoutMs = 60_000;

    private IPlaywright? _playwright;
    private Process? _appProcess;
    private IBrowser? _browser;
    private string _userDataFolder = string.Empty;
    private string _processLogPath = string.Empty;
    private int _cdpPort;
    private CancellationTokenSource? _logCaptureCts;

    // In-memory log lines for cursor-based query API.
    private readonly List<string> _logLines = [];
    private readonly object _logLock = new();

    /// <summary>The Playwright page connected to the Monaco WebView2 content.</summary>
    public IPage Page { get; private set; } = null!;

    /// <summary>The Playwright browser context for tracing support.</summary>
    public IBrowserContext Context { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // 0. Create Playwright instance (owned by this fixture).
        _playwright = await Playwright.CreateAsync();

        // 1. Pick a random available port for CDP.
        _cdpPort = GetAvailablePort();

        // 2. Create unique user data folder per test run to prevent parallel interference.
        _userDataFolder = Path.Combine(Path.GetTempPath(), $"uno-monaco-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_userDataFolder);

        // 3. Ensure test-artifacts directory exists.
        var artifactsDir = Path.Combine(FindRepoRoot(), "test-artifacts");
        Directory.CreateDirectory(artifactsDir);
        _processLogPath = Path.Combine(artifactsDir, $"desktop-fixture-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");

        // 4. Launch MonacoEditorTestApp desktop with CDP enabled.
        var repoRoot = FindRepoRoot();
        var testAppProject = Path.Combine(repoRoot, "MonacoEditorTestApp", "MonacoEditorTestApp.csproj");

        // Use -c Release --no-build to run the pre-built app without triggering a Debug
        // rebuild. CI pre-builds with -c Release (ci.yml desktop-tests job), so omitting
        // -c Release here caused dotnet run to rebuild in Debug, eating ~30s of timeout.
        // --no-launch-profile prevents launch profile env vars from interfering.
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{testAppProject}\" -f net10.0-desktop -c Release --no-build --no-launch-profile",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Environment =
            {
                ["WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS"] = $"--remote-debugging-port={_cdpPort}",
                ["WEBVIEW2_USER_DATA_FOLDER"] = _userDataFolder,
                ["MONACO_DIAGNOSTICS"] = "1",
            },
        };

        _appProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start MonacoEditorTestApp desktop process.");

        // Start background log capture with cancellation support.
        _logCaptureCts = new CancellationTokenSource();
        _ = CaptureProcessOutputAsync(_appProcess, _processLogPath, _logCaptureCts.Token);

        // 5. Deterministic readiness: poll CDP version endpoint.
        var cdpEndpoint = $"http://localhost:{_cdpPort}";
        await WaitForCdpReady(cdpEndpoint);

        // 6. Connect Playwright via CDP.
        _browser = await _playwright!.Chromium.ConnectOverCDPAsync(cdpEndpoint);

        // 7. Find the Monaco page.
        Page = await FindMonacoPage();
        Context = Page.Context;

        // 8. Start tracing for failure artifact collection.
        await Context.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
        });

        // 9. Wait for Monaco to be ready in the page.
        await Page.WaitForFunctionAsync(
            "() => typeof monaco !== 'undefined' && monaco.editor.getEditors().length > 0",
            null, new PageWaitForFunctionOptions { Timeout = MonacoReadyTimeoutMs });
    }

    public async ValueTask DisposeAsync()
    {
        // Cancel log capture first so stream reads don't block process disposal.
        if (_logCaptureCts is not null)
        {
            try { await _logCaptureCts.CancelAsync(); } catch { /* best-effort */ }
            _logCaptureCts.Dispose();
            _logCaptureCts = null;
        }

        // Kill the app process FIRST so the browser close doesn't wait for it.
        // On Windows, Playwright browser.CloseAsync can hang indefinitely if the
        // underlying CDP target (WebView2) is unresponsive. Killing the process
        // before closing the browser ensures deterministic cleanup.
        if (_appProcess is { HasExited: false })
        {
            try
            {
                _appProcess.Kill(entireProcessTree: true);
                await _appProcess.WaitForExitAsync(new CancellationTokenSource(5000).Token);
            }
            catch { /* best-effort cleanup */ }
        }

        if (_browser is not null)
        {
            try
            {
                // Use a timeout to prevent hanging if the browser close is blocked.
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _browser.CloseAsync().WaitAsync(closeCts.Token);
            }
            catch { /* best-effort cleanup */ }
        }

        _appProcess?.Dispose();
        _playwright?.Dispose();

        // Clean up unique user data folder.
        if (!string.IsNullOrEmpty(_userDataFolder) && Directory.Exists(_userDataFolder))
        {
            try { Directory.Delete(_userDataFolder, recursive: true); } catch { /* best-effort */ }
        }
    }

    // ============================================================
    // Editor state reset helper
    // ============================================================

    /// <summary>
    /// Resets the editor to the known initial state matching the host-initiated startup values
    /// (<c>Text = "// test-init-text"</c>, <c>CodeLanguage = "javascript"</c>, theme = "vs").
    /// Call at the start of each test (not teardown) so failures are immediately visible.
    /// </summary>
    public async Task ResetEditorStateAsync()
    {
        await Page.EvaluateAsync("""
            () => {
                const editor = monaco.editor.getEditors()[0];
                // Reset text
                editor.setValue('// test-init-text');
                // Reset language
                const model = editor.getModel();
                if (model) {
                    monaco.editor.setModelLanguage(model, 'javascript');
                    // Clear markers
                    monaco.editor.setModelMarkers(model, 'test', []);
                    monaco.editor.setModelMarkers(model, 'CodeEditor', []);
                }
                // Reset theme
                monaco.editor.setTheme('vs');
                // Reset decorations
                editor.deltaDecorations(
                    editor.getModel().getAllDecorations().map(d => d.id),
                    []
                );
                // Reset read-only
                editor.updateOptions({ readOnly: false, glyphMargin: true });
            }
            """);
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
    /// Captures a failure screenshot and stops the Playwright trace, saving artifacts
    /// to the <c>test-artifacts/</c> directory. Call from test teardown on failure.
    /// </summary>
    public async Task CaptureFailureArtifacts(string testName)
    {
        var artifactsDir = Path.Combine(FindRepoRoot(), "test-artifacts");
        Directory.CreateDirectory(artifactsDir);

        try
        {
            await Page.ScreenshotAsync(new()
            {
                Path = Path.Combine(artifactsDir, $"{testName}-failure.png"),
            });
        }
        catch { /* best-effort */ }

        try
        {
            await Context.Tracing.StopAsync(new()
            {
                Path = Path.Combine(artifactsDir, $"{testName}-trace.zip"),
            });

            // Restart tracing so subsequent failing tests also get trace artifacts.
            await Context.Tracing.StartAsync(new()
            {
                Screenshots = true,
                Snapshots = true,
            });
        }
        catch { /* best-effort */ }

        // Copy process log to test-specific path.
        if (File.Exists(_processLogPath))
        {
            try
            {
                File.Copy(_processLogPath, Path.Combine(artifactsDir, $"{testName}-process.log"), overwrite: true);
            }
            catch { /* best-effort */ }
        }
    }

    private async Task WaitForCdpReady(string cdpEndpoint)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var versionUrl = $"{cdpEndpoint}/json/version";
        var deadline = DateTime.UtcNow.AddMilliseconds(CdpReadyTimeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (_appProcess is { HasExited: true })
            {
                var exitCode = _appProcess.ExitCode;
                var logContent = File.Exists(_processLogPath) ? File.ReadAllText(_processLogPath) : "(no log)";
                throw new InvalidOperationException(
                    $"MonacoEditorTestApp process exited unexpectedly with code {exitCode} before CDP was ready.\n" +
                    $"Process log:\n{logContent}");
            }

            try
            {
                var response = await httpClient.GetAsync(versionUrl);
                if (response.IsSuccessStatusCode)
                {
                    return; // CDP is ready.
                }
            }
            catch (HttpRequestException)
            {
                // Not ready yet -- keep polling.
            }
            catch (TaskCanceledException)
            {
                // Request timed out -- keep polling.
            }

            await Task.Delay(CdpPollIntervalMs);
        }

        var logOnTimeout = File.Exists(_processLogPath) ? File.ReadAllText(_processLogPath) : "(no log)";
        throw new TimeoutException(
            $"CDP endpoint at {versionUrl} did not become ready within {CdpReadyTimeoutMs}ms.\n" +
            $"Process log:\n{logOnTimeout}");
    }

    private async Task<IPage> FindMonacoPage()
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(MonacoPageTimeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (_browser?.Contexts.Count > 0)
            {
                foreach (var context in _browser.Contexts)
                {
                    foreach (var page in context.Pages)
                    {
                        // The Monaco editor page will have a URL containing the virtual host
                        // or a file:// URL pointing to the editor HTML.
                        var url = page.Url;
                        if (url.Contains("uno-monaco", StringComparison.OrdinalIgnoreCase) ||
                            url.Contains("monaco", StringComparison.OrdinalIgnoreCase) ||
                            url.Contains("index.html", StringComparison.OrdinalIgnoreCase))
                        {
                            return page;
                        }
                    }

                    // If no URL match, return first non-blank page.
                    foreach (var page in context.Pages)
                    {
                        if (!page.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                        {
                            return page;
                        }
                    }
                }
            }

            await Task.Delay(500);
        }

        // Last resort: return whatever page exists.
        if (_browser?.Contexts.Count > 0 && _browser.Contexts[0].Pages.Count > 0)
        {
            return _browser.Contexts[0].Pages[0];
        }

        throw new TimeoutException(
            $"Could not find Monaco editor page within {MonacoPageTimeoutMs}ms. " +
            "Check that the desktop app started correctly and WebView2 loaded the editor.");
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepoRoot()
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

            // ReadLineAsync returns null at end-of-stream, avoiding the CA2024
            // diagnostic from synchronous EndOfStream checks in async methods.
            var stdoutTask = Task.Run(async () =>
            {
                string? line;
                while (!cancellationToken.IsCancellationRequested &&
                       (line = await process.StandardOutput.ReadLineAsync(cancellationToken)) is not null)
                {
                    var formattedLine = $"[stdout] {line}";
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
            var stderrTask = Task.Run(async () =>
            {
                string? line;
                while (!cancellationToken.IsCancellationRequested &&
                       (line = await process.StandardError.ReadLineAsync(cancellationToken)) is not null)
                {
                    var formattedLine = $"[stderr] {line}";
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

            await Task.WhenAny(Task.WhenAll(stdoutTask, stderrTask), Task.Delay(TimeSpan.FromMinutes(5), cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Expected when fixture is being disposed -- log capture is no longer needed.
        }
        catch
        {
            // Best-effort log capture -- never throw from background task.
        }
    }
}
