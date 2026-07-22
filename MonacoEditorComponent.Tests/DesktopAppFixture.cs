using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
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
/// It polls <c>http://127.0.0.1:{port}/json/version</c> to confirm CDP is ready,
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
        EnsurePlaywrightDriverSearchPath();
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

        // WebView2 runtime v150 disables the CDP loopback endpoint when the host runs at
        // High Integrity Level (elevated), which is how the windows-latest CI runner
        // executes -- so --remote-debugging-port silently produces no listener there
        // (WebView2Feedback #5640). When CI provides a pre-v150 Fixed Version runtime via
        // WEBVIEW2_BROWSER_EXECUTABLE_FOLDER, forward it so the app uses that runtime and
        // CDP works again. Unset locally (developers run non-elevated) => Evergreen runtime.
        var fixedRuntimeFolder = Environment.GetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER");
        if (!string.IsNullOrEmpty(fixedRuntimeFolder))
        {
            startInfo.Environment["WEBVIEW2_BROWSER_EXECUTABLE_FOLDER"] = fixedRuntimeFolder;
        }

        _appProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start MonacoEditorTestApp desktop process.");

        // Start background log capture with cancellation support.
        _logCaptureCts = new CancellationTokenSource();
        _ = CaptureProcessOutputAsync(_appProcess, _processLogPath, _logCaptureCts.Token);

        // 5. Deterministic readiness: poll CDP version endpoint.
        // Use the IPv4 loopback literal, not "localhost": WebView2/Chromium's
        // --remote-debugging-port (with no --remote-debugging-address) binds to
        // 127.0.0.1 only. On runners where "localhost" resolves to IPv6 (::1) first,
        // the CDP endpoint is unreachable (connection refused) and readiness polling
        // times out even though the app itself started fine. This also matches the
        // port reserved via IPAddress.Loopback in GetAvailablePort(). The endpoint
        // feeds both the readiness poll below and ConnectOverCDPAsync in step 6.
        var cdpEndpoint = $"http://127.0.0.1:{_cdpPort}";
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

        // 10. Wait for the test harness to complete all async setup (command/action
        // registration, language registration, markers, decorations, theme switching).
        // The harness emits TEST_HARNESS_READY as its final stdout marker.
        await WaitForLogLineAfterAsync(0, @"TEST_HARNESS_READY", MonacoReadyTimeoutMs);
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
                    // Clear markers (all known owners used by tests and harness)
                    monaco.editor.setModelMarkers(model, 'test', []);
                    monaco.editor.setModelMarkers(model, 'CodeEditor', []);
                    monaco.editor.setModelMarkers(model, 'testHarness', []);
                    monaco.editor.setModelMarkers(model, 'cdpTest', []);
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
                throw new InvalidOperationException(
                    $"MonacoEditorTestApp process exited unexpectedly with code {exitCode} before CDP was ready.\n" +
                    $"Process log:\n{CaptureLogSnapshot()}");
            }

            try
            {
                var response = await httpClient.GetAsync(versionUrl);
                if (response.IsSuccessStatusCode)
                {
                    // Record the runtime that actually answered CDP (the "Browser" field,
                    // e.g. "Edg/149.0.4022.98"). This is authoritative for which WebView2
                    // runtime loaded: the machine's Evergreen version in the registry is
                    // not, once a Fixed Version runtime is pinned via
                    // WEBVIEW2_BROWSER_EXECUTABLE_FOLDER (WebView2Feedback #5640).
                    try
                    {
                        var versionBody = await response.Content.ReadAsStringAsync();
                        var dir = Path.GetDirectoryName(_processLogPath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            await File.WriteAllTextAsync(Path.Combine(dir, "cdp-version.txt"), versionBody);
                        }
                    }
                    catch { /* best-effort */ }
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

        var cdpDiagnostics = await CollectCdpDiagnosticsAsync();
        throw new TimeoutException(
            $"CDP endpoint at {versionUrl} did not become ready within {CdpReadyTimeoutMs}ms.\n" +
            $"CDP diagnostics:\n{cdpDiagnostics}\n" +
            $"Process log:\n{CaptureLogSnapshot()}");
    }

    /// <summary>
    /// Best-effort, Windows-only diagnostics gathered when the CDP endpoint never became
    /// ready. Answers the key question the readiness timeout cannot: did WebView2/Chromium
    /// even start a remote-debugging listener, and if not, why?
    /// <list type="bullet">
    /// <item><description><c>DevToolsActivePort</c> present under the user-data folder =&gt;
    /// the listener started (line 1 is the actual port; if it differs from the requested
    /// port, the fixture polled the wrong one).</description></item>
    /// <item><description>Absent =&gt; remote debugging never started (policy or the
    /// <c>WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS</c> flag was not honored).</description></item>
    /// <item><description>Edge <c>RemoteDebuggingAllowed</c> policy = 0 disables CDP entirely.</description></item>
    /// </list>
    /// Also written to <c>test-artifacts/cdp-diagnostics.txt</c> for the uploaded artifact.
    /// </summary>
    private async Task<string> CollectCdpDiagnosticsAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "(CDP diagnostics collected on Windows only)";
        }

        var sb = new StringBuilder();

        // 0. Integrity level of this process (== the spawned app's IL, since the app is
        //    launched as a child). WebView2 runtime v150 disables the CDP loopback
        //    endpoint when the host runs at High Mandatory Level (WebView2Feedback #5640).
        sb.AppendLine("--- process integrity level (whoami /groups | Mandatory) ---")
          .AppendLine(await RunCaptureAsync("cmd", "/c whoami /groups | findstr /i Mandatory"));

        // 1. DevToolsActivePort: the decisive signal for whether a listener started.
        try
        {
            var found = Directory.Exists(_userDataFolder)
                ? Directory.GetFiles(_userDataFolder, "DevToolsActivePort", SearchOption.AllDirectories)
                : [];
            if (found.Length == 0)
            {
                sb.AppendLine("DevToolsActivePort: NOT FOUND under user-data folder " +
                    "=> remote debugging never started (arg/policy not honored).");
            }
            else
            {
                foreach (var file in found)
                {
                    // Read with FileShare.ReadWrite in case Chromium still holds it open.
                    using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(fs);
                    var content = (await reader.ReadToEndAsync()).Trim();
                    sb.AppendLine($"DevToolsActivePort ({file}):").AppendLine(content)
                      .AppendLine($"(requested port was {_cdpPort})");
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"DevToolsActivePort probe failed: {ex.Message}");
        }

        // 2. Edge remote-debugging policy (0 => CDP disabled by policy).
        sb.AppendLine("--- Edge policies (HKLM) ---")
          .AppendLine(await RunCaptureAsync("reg", @"query ""HKLM\SOFTWARE\Policies\Microsoft\Edge"" /s"));
        sb.AppendLine("--- Edge policies (HKCU) ---")
          .AppendLine(await RunCaptureAsync("reg", @"query ""HKCU\SOFTWARE\Policies\Microsoft\Edge"" /s"));

        // 3. Is anything listening on the requested port?
        sb.AppendLine($"--- netstat (port {_cdpPort}) ---")
          .AppendLine(await RunCaptureAsync("cmd", $"/c netstat -ano -p tcp | findstr {_cdpPort}"));

        // 4. WebView2 Evergreen runtime version.
        sb.AppendLine("--- WebView2 runtime version ---")
          .AppendLine(await RunCaptureAsync("reg",
              @"query ""HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"" /v pv"));

        var diagnostics = sb.ToString();

        // Persist to the uploaded artifacts directory for reliable capture.
        try
        {
            var dir = Path.GetDirectoryName(_processLogPath);
            if (!string.IsNullOrEmpty(dir))
            {
                await File.WriteAllTextAsync(Path.Combine(dir, "cdp-diagnostics.txt"), diagnostics);
            }
        }
        catch { /* best-effort */ }

        return diagnostics;
    }

    /// <summary>Runs a console command and returns combined stdout+stderr (best-effort).</summary>
    private static async Task<string> RunCaptureAsync(string fileName, string arguments, int timeoutMs = 10_000)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            proc.Start();
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(timeoutMs);
            try { await proc.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException) { try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ } }
            var output = ($"{await stdoutTask}{await stderrTask}").Trim();
            return string.IsNullOrEmpty(output) ? "(no output)" : output;
        }
        catch (Exception ex)
        {
            return $"(command '{fileName} {arguments}' failed: {ex.Message})";
        }
    }

    /// <summary>
    /// Returns the captured process stdout/stderr as a single string from the in-memory
    /// buffer. Reading the on-disk log file directly is unsafe on Windows: the background
    /// <see cref="CaptureProcessOutputAsync"/> writer holds it open, and a concurrent
    /// reader's implicit FileShare.Read does not grant the writer's Write access, which
    /// surfaces as an IOException that masks the real readiness diagnostic.
    /// </summary>
    private string CaptureLogSnapshot()
    {
        var lines = GetLinesAfter(0);
        return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "(no log)";
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

    /// <summary>
    /// Points Playwright at the driver bundled in the Microsoft.Playwright NuGet package
    /// (the .playwright/ folder) when <c>PLAYWRIGHT_DRIVER_SEARCH_PATH</c> is not already set.
    /// The project excludes Playwright's build assets (see the .csproj), so the driver is not
    /// copied next to the test DLL; CI sets this env var explicitly, and this makes local runs
    /// work without manual setup. No-op when the env var is already set (e.g. on CI).
    /// </summary>
    private static void EnsurePlaywrightDriverSearchPath()
    {
        const string envVar = "PLAYWRIGHT_DRIVER_SEARCH_PATH";
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar)))
        {
            return;
        }

        var packagePath = typeof(DesktopAppFixture).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "PlaywrightPackagePath")?.Value;

        if (!string.IsNullOrEmpty(packagePath) && Directory.Exists(packagePath))
        {
            Environment.SetEnvironmentVariable(envVar, packagePath);
        }
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
