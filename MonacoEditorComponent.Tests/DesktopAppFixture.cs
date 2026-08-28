using System.ComponentModel;
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
/// using the Playwright MCP server with <c>--cdp-endpoint http://127.0.0.1:{port}</c>.
/// This provides <c>browser_snapshot</c> for accessibility tree inspection,
/// <c>browser_evaluate</c> for JS assertions, and <c>browser_click</c> for
/// interaction testing. This is a development convenience, not automated CI.</para>
/// </summary>
public sealed class DesktopAppFixture : IAsyncLifetime
{
    private const int CdpPollIntervalMs = 500;
    private const int CdpReadyTimeoutMs = 30_000;
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
    private bool _wroteHklmBrowserArgs;

    // Machine-wide WebView2 policy key. Arguments published here survive an elevated host
    // process, unlike the WEBVIEW2_* environment variables (WebView2Feedback #5640).
    // Per the WebView2 loader contract, AdditionalBrowserArguments is a SUBKEY whose value
    // is named by the AppId (AUMID / compiled code name), with "*" as the all-apps wildcard --
    // NOT a value named "AdditionalBrowserArguments" under the WebView2 key. Writing it at the
    // wrong level is silently ignored by the loader. See CreateCoreWebView2EnvironmentWithOptions.
    private const string WebView2PolicyKey = @"HKLM\SOFTWARE\Policies\Microsoft\Edge\WebView2";
    private const string WebView2AdditionalArgsKey = WebView2PolicyKey + @"\AdditionalBrowserArguments";
    private const string WebView2AppIdWildcard = "*";

    // In-memory log lines for cursor-based query API.
    private readonly List<string> _logLines = [];
    private readonly object _logLock = new();

    /// <summary>The Playwright page connected to the Monaco WebView2 content.</summary>
    public IPage Page { get; private set; } = null!;

    /// <summary>
    /// The page hosting the <c>DiffCodeEditor</c> sample. On desktop every editor control owns
    /// its own WebView2, so this is a different page from <see cref="Page"/>.
    /// </summary>
    public IPage DiffPage { get; private set; } = null!;

    /// <summary>The Playwright browser context for tracing support.</summary>
    public IBrowserContext Context { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // 0. Create Playwright instance (owned by this fixture).
        PlaywrightDriverPath.Ensure();
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
                // Realizes the DiffCodeEditor sample in an always-visible panel. A diff editor
                // parked in a TabView tab is never constructed until a human selects the tab,
                // and CDP drives WebView contents rather than the XAML tree, so there would be
                // no way to reach it from a test.
                ["MONACO_DIFF_TAB"] = "1",
            },
        };

        // WebView2 runtime v150 hardened elevated (High Integrity Level) hosts: switches
        // supplied through user-writable channels (WEBVIEW2_* env vars, HKCU policy) are
        // ignored -- only HKLM policy and API args are honored (WebView2Feedback #5640,
        // "by design"). The windows-latest CI runner executes elevated, so the
        // --remote-debugging-port set above via WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS is
        // silently dropped and no CDP listener starts. There, additionally publish the
        // switch through the HKLM AdditionalBrowserArguments policy, which survives elevation.
        //
        // Restrict this machine-wide write to CI (GITHUB_ACTIONS) on top of the integrity
        // check: writing HKLM policy affects other WebView2 hosts on the machine and could
        // linger if the run is interrupted before DisposeAsync reverts it. CI runners are
        // ephemeral, so that is acceptable there; a developer running elevated locally is not
        // modified (they keep the env var, which is enough at Medium IL / pre-v150 anyway).
        var isCi = string.Equals(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);
        if (isCi && await IsHostHighIntegrityAsync())
        {
            await PublishHklmBrowserArgumentsAsync($"--remote-debugging-port={_cdpPort}");
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

        // 7. Find the Monaco pages. Two WebView2 hosts are live (the plain editor sample and
        // the diff sample) and they share a URL, so they are told apart by content.
        // Excluding diff editors is not redundant: monaco.editor.getEditors() lists every code
        // editor the service knows about, and a diff widget's two sub-editors are standalone
        // code editors, so the diff page can satisfy a bare getEditors() probe too.
        Page = await FindEditorPageAsync(
            "monaco.editor.getEditors().length > 0 && monaco.editor.getDiffEditors().length === 0",
            "plain editor");
        DiffPage = await FindEditorPageAsync(
            "monaco.editor.getDiffEditors().length > 0", "diff editor");
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

        // Revert the machine-wide WebView2 policy set for elevated CDP (WebView2Feedback #5640).
        if (_wroteHklmBrowserArgs)
        {
            try
            {
                await RunCaptureAsync("reg", $@"delete ""{WebView2AdditionalArgsKey}"" /v {WebView2AppIdWildcard} /f");
            }
            catch (InvalidOperationException) { /* best-effort */ }
            catch (Win32Exception) { /* best-effort */ }
            _wroteHklmBrowserArgs = false;
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
                using var response = await httpClient.GetAsync(versionUrl);
                if (response.IsSuccessStatusCode)
                {
                    // Record the runtime that actually answered CDP (the "Browser" field,
                    // e.g. "Edg/150.0.4078.65") to test-artifacts. Useful confirmation of
                    // exactly which WebView2 runtime served the session.
                    try
                    {
                        var versionBody = await response.Content.ReadAsStringAsync();
                        var versionPath = ArtifactSiblingPath("cdp-version.txt");
                        if (versionPath is not null)
                        {
                            await File.WriteAllTextAsync(versionPath, versionBody);
                        }
                    }
                    catch (IOException) { /* best-effort */ }
                    catch (UnauthorizedAccessException) { /* best-effort */ }
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
    /// Also written to <c>test-artifacts/{run}-cdp-diagnostics.txt</c> for the uploaded artifact.
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
        catch (IOException ex)
        {
            // Covers DirectoryNotFound/FileNotFound/PathTooLong (all derive from IOException).
            sb.AppendLine($"DevToolsActivePort probe failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            sb.AppendLine($"DevToolsActivePort probe failed: {ex.Message}");
        }

        // 2. Edge remote-debugging policy (0 => CDP disabled by policy). Query the specific
        //    value rather than dumping the whole policy tree (/s), which is large and noisy.
        //    A "unable to find" result means the policy is unset (i.e. not disabling CDP).
        sb.AppendLine("--- Edge RemoteDebuggingAllowed (HKLM) ---")
          .AppendLine(await RunCaptureAsync("reg", @"query ""HKLM\SOFTWARE\Policies\Microsoft\Edge"" /v RemoteDebuggingAllowed"));
        sb.AppendLine("--- Edge RemoteDebuggingAllowed (HKCU) ---")
          .AppendLine(await RunCaptureAsync("reg", @"query ""HKCU\SOFTWARE\Policies\Microsoft\Edge"" /v RemoteDebuggingAllowed"));

        // 2b. The AdditionalBrowserArguments we publish via HKLM policy for elevated CDP
        //     (WebView2Feedback #5640). Present with our --remote-debugging-port => the
        //     write took effect (so the runtime is ignoring HKLM); absent => the write was
        //     skipped (gate) or failed (perms). Read while still live (DisposeAsync reverts).
        sb.AppendLine("--- HKLM WebView2 AdditionalBrowserArguments\\* (elevated CDP write) ---")
          .AppendLine(await RunCaptureAsync("reg", $@"query ""{WebView2AdditionalArgsKey}"" /v {WebView2AppIdWildcard}"));

        // 3. Is anything listening on the requested port? Match the exact ":{port}" token
        //    (port followed by a non-digit) so e.g. 5000 does not also match 15000/50001.
        sb.AppendLine($"--- netstat (port {_cdpPort}) ---")
          .AppendLine(await RunCaptureAsync("cmd", $"/c netstat -ano -p tcp | findstr /r \":{_cdpPort}[^0-9]\""));

        // 4. WebView2 Evergreen runtime version.
        sb.AppendLine("--- WebView2 runtime version ---")
          .AppendLine(await RunCaptureAsync("reg",
              @"query ""HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"" /v pv"));

        var diagnostics = sb.ToString();

        // Persist to the uploaded artifacts directory for reliable capture.
        try
        {
            var diagnosticsPath = ArtifactSiblingPath("cdp-diagnostics.txt");
            if (diagnosticsPath is not null)
            {
                await File.WriteAllTextAsync(diagnosticsPath, diagnostics);
            }
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }

        return diagnostics;
    }

    /// <summary>Runs a console command and returns combined stdout+stderr (best-effort).</summary>
    private static async Task<string> RunCaptureAsync(string fileName, string arguments, int timeoutMs = 10_000)
    {
        string Failed(Exception ex) => $"(command '{fileName} {arguments}' failed: {ex.Message})";

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

            // Bound the stream reads by the same timeout token as the exit wait so a
            // process that never exits (or whose Kill fails) can't hang log collection
            // indefinitely -- the reads are cancelled when the token fires.
            using var cts = new CancellationTokenSource(timeoutMs);
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = proc.StandardError.ReadToEndAsync(cts.Token);
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* already exited */ }
                catch (Win32Exception) { /* OS refused termination */ }
                catch (NotSupportedException) { /* unsupported on platform */ }
            }

            var output = $"{await ReadOrEmptyAsync(stdoutTask)}{await ReadOrEmptyAsync(stderrTask)}".Trim();
            return string.IsNullOrEmpty(output) ? "(no output)" : output;
        }
        catch (Win32Exception ex) { return Failed(ex); }
        catch (InvalidOperationException ex) { return Failed(ex); }
        catch (IOException ex) { return Failed(ex); }
    }

    /// <summary>Awaits a bounded stream read, returning empty on cancellation/I-O failure (best-effort).</summary>
    private static async Task<string> ReadOrEmptyAsync(Task<string> readTask)
    {
        try { return await readTask; }
        catch (OperationCanceledException) { return string.Empty; }
        catch (IOException) { return string.Empty; }
    }

    /// <summary>
    /// Builds an artifact path sharing the timestamped base name of the process log
    /// (e.g. <c>desktop-fixture-{timestamp}-cdp-version.txt</c>) so a run's diagnostics group
    /// together and don't overwrite each other across parallel fixture instances or retries.
    /// Returns null when the log directory cannot be determined.
    /// </summary>
    private string? ArtifactSiblingPath(string suffix)
    {
        var dir = Path.GetDirectoryName(_processLogPath);
        if (string.IsNullOrEmpty(dir))
        {
            return null;
        }
        return Path.Join(dir, $"{Path.GetFileNameWithoutExtension(_processLogPath)}-{suffix}");
    }

    /// <summary>
    /// True when the current process runs at High (or System) Integrity Level on Windows.
    /// Uses the mandatory-label SID from <c>whoami /groups</c> rather than
    /// <see cref="Environment.IsPrivilegedProcess"/>, which keys off UAC token elevation and
    /// returns false on CI runners where UAC is disabled but the process is still High IL --
    /// the exact condition under which WebView2 v150 drops the WEBVIEW2_* switches (#5640).
    /// </summary>
    private static async Task<bool> IsHostHighIntegrityAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
        var groups = await RunCaptureAsync("whoami", "/groups");
        return groups.Contains("S-1-16-12288", StringComparison.Ordinal)   // High
            || groups.Contains("S-1-16-16384", StringComparison.Ordinal);  // System
    }

    /// <summary>
    /// Publishes WebView2 browser arguments through the machine-wide (HKLM) policy so they
    /// survive an elevated host process. WebView2 v150 ignores the user-writable channels
    /// (WEBVIEW2_* environment variables, HKCU policy) when the host runs at High Integrity
    /// Level -- only HKLM policy and API args are honored (WebView2Feedback #5640). Called
    /// only when elevated (the sole case that needs it); best-effort and reverted in
    /// <see cref="DisposeAsync"/>. A pre-existing machine policy value is left untouched.
    /// </summary>
    /// <remarks>
    /// SINGLE-WRITER ASSUMPTION: the <c>*</c> wildcard is one machine-global registry slot, but
    /// each fixture instance picks its own <c>_cdpPort</c>. The write/skip/revert dance here is
    /// race-free ONLY because the desktop tests share a single, serialized fixture instance via
    /// <c>[Collection("DesktopCDP")]</c> + <c>ICollectionFixture&lt;DesktopAppFixture&gt;</c> (see
    /// <c>DesktopCdpCollection</c>). If these tests are ever moved to a per-class
    /// <c>IClassFixture</c> or split across parallel collections, two concurrent instances would
    /// collide on this slot: the second would find the first's value, skip its own write, inherit
    /// the wrong port and time out, and whichever disposes first would delete the value out from
    /// under the other. Such a refactor must key the value per app/port (or otherwise serialize
    /// this write) instead of using <c>*</c>.
    /// </remarks>
    private async Task PublishHklmBrowserArgumentsAsync(string arguments)
    {
        // Do not clobber a wildcard value the machine already defines (the CI runner defines none).
        // Detect it via the REG_ type token on the query's value line, not a bare substring:
        // RunCaptureAsync's own failure text echoes the queried command, so a substring check
        // would read a reg-launch failure as "value already exists" and skip the write.
        var existing = await RunCaptureAsync("reg", $@"query ""{WebView2AdditionalArgsKey}"" /v {WebView2AppIdWildcard}");
        if (Regex.IsMatch(existing, @"\bREG_", RegexOptions.IgnoreCase))
        {
            return;
        }

        // Set the flag before writing so DisposeAsync reverts the machine policy even if the
        // verification below throws; deleting an absent value is a harmless no-op.
        _wroteHklmBrowserArgs = true;
        var addOutput = await RunCaptureAsync("reg",
            $@"add ""{WebView2AdditionalArgsKey}"" /v {WebView2AppIdWildcard} /t REG_SZ /d ""{arguments}"" /f");

        // reg can fail without throwing (e.g. access denied / locked-down policy) -- RunCaptureAsync
        // just returns its output. Confirm the value actually landed and fail fast with the reg
        // output, rather than letting CDP readiness time out ~30s later with an indirect error.
        var verify = await RunCaptureAsync("reg", $@"query ""{WebView2AdditionalArgsKey}"" /v {WebView2AppIdWildcard}");
        if (!Regex.IsMatch(verify, @"\bREG_", RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException(
                "Failed to publish the HKLM WebView2 AdditionalBrowserArguments policy required for elevated " +
                $"CDP (WebView2Feedback #5640).{Environment.NewLine}reg add output: {addOutput}{Environment.NewLine}" +
                $"reg query output: {verify}");
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
    /// Finds the WebView2 page whose Monaco instance satisfies <paramref name="readyExpression"/>.
    /// </summary>
    /// <remarks>
    /// Probes page content rather than matching URLs: the app hosts more than one WebView2 and
    /// they all navigate to the same editor.html, so a URL heuristic picks one at random. The
    /// probe also doubles as a readiness wait, since a page that has not finished loading
    /// Monaco simply fails to match.
    /// </remarks>
    private async Task<IPage> FindEditorPageAsync(string readyExpression, string description)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(MonacoReadyTimeoutMs);
        var probe = $"() => typeof monaco !== 'undefined' && {readyExpression}";

        while (DateTime.UtcNow < deadline)
        {
            foreach (var page in SnapshotPages())
            {
                try
                {
                    if (page.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (await page.EvaluateAsync<bool>(probe))
                    {
                        return page;
                    }
                }
                catch (PlaywrightException)
                {
                    // The page can be mid-navigation, not yet executing scripts, or already
                    // gone since the snapshot was taken; the next sweep retries it.
                    // PlaywrightException is the base of every failure this body can raise --
                    // evaluation errors, destroyed execution contexts and TargetClosedException
                    // all derive from it.
                }
            }

            await Task.Delay(CdpPollIntervalMs);
        }

        var seen = string.Join(", ", SnapshotPages().Select(p => p.Url));
        throw new TimeoutException(
            $"Could not find the {description} page within {MonacoReadyTimeoutMs}ms. " +
            $"Pages last seen: [{seen}]. Check that the desktop app started and WebView2 loaded the editor.");
    }

    /// <summary>
    /// Takes a point-in-time copy of every page across the browser's contexts.
    /// </summary>
    /// <remarks>
    /// <see cref="IBrowser.Contexts"/> and <see cref="IBrowserContext.Pages"/> hand back the
    /// driver's own backing lists, and the driver appends to them from its dispatch loop as the
    /// app's WebView2 hosts attach. Enumerating them directly across an await therefore throws
    /// "Collection was modified", which is what took this fixture down in CI once the diff
    /// sample added a second host that attaches while the first sweep is in flight. Copying
    /// trades that for a possibly stale page reference, which callers already tolerate: every
    /// call they make on a page is inside a <see cref="PlaywrightException"/> catch, and
    /// TargetClosedException derives from it. The copy itself takes no enumerator over a live
    /// list -- ToArray on a List-backed IReadOnlyList is a span/CopyTo bulk copy.
    /// </remarks>
    private IReadOnlyList<IPage> SnapshotPages() =>
        (_browser?.Contexts ?? []).ToArray()
            .SelectMany(context => context.Pages.ToArray())
            .ToArray();


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
