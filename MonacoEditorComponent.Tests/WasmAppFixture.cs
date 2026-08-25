using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using Microsoft.Playwright;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// xUnit fixture that serves a pre-built WASM app via a static file server
/// and connects Playwright Chromium for browser-based integration testing.
///
/// <para><b>Build precondition</b>: The fixture resolves the WASM build output from
/// <c>MonacoEditorTestApp/bin/Release/net10.0-browserwasm/wwwroot/</c> first,
/// falling back to <c>Debug</c>. If neither exists, the fixture fails fast with
/// a clear error instructing the developer to build the WASM target.</para>
///
/// <para><b>Runs on any OS</b>: Unlike the desktop CDP tests, WASM tests use
/// standard Playwright browser automation and run on ubuntu-latest in CI.</para>
/// </summary>
public sealed class WasmAppFixture : IAsyncLifetime
{
    private const int MonacoReadyTimeoutMs = 30_000;

    /// <summary>
    /// Emitted by <c>EditorControl</c> once both of its <c>async void</c> init handlers have
    /// finished and the last write it issued has been observed landing in Monaco.
    /// </summary>
    private const string AppInitSettledMarker = "APP_INIT_SETTLED";

    /// <summary>Content every test starts from -- see <see cref="ResetEditorStateAsync"/>.</summary>
    private const string ResetText = "// test-init-text";

    private IPlaywright? _playwright;
    private Process? _serverProcess;
    private IBrowser? _browser;
    private string _processLogPath = string.Empty;
    private int _serverPort;

    private readonly List<string> _consoleLines = [];
    private readonly object _consoleLock = new();

    private readonly TaskCompletionSource<string> _appInitComplete =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The Playwright page connected to the WASM app.</summary>
    public IPage Page { get; private set; } = null!;

    /// <summary>The Playwright browser context for tracing support.</summary>
    public IBrowserContext Context { get; private set; } = null!;

    /// <summary>
    /// The editor text the app settled on during its own initialisation, captured once before any
    /// test ran. Tests asserting on first-load content must use this rather than reading the live
    /// model, which every preceding test in the collection is free to overwrite.
    /// </summary>
    public string InitialEditorText { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var repoRoot = FindRepoRoot();

        // 0. Create Playwright instance (owned by this fixture).
        _playwright = await Playwright.CreateAsync();

        // 1. Resolve WASM build output directory (Release then Debug fallback).
        var wwwrootPath = ResolveWasmBuildOutput(repoRoot);

        // 2. Pick a random available port.
        _serverPort = GetAvailablePort();

        // 3. Ensure test-artifacts directory exists.
        var artifactsDir = Path.Combine(repoRoot, "test-artifacts");
        Directory.CreateDirectory(artifactsDir);
        _processLogPath = Path.Combine(artifactsDir, $"wasm-fixture-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");

        // 4. Start a static file server on the wwwroot path.
        // Use dotnet-serve if available, fallback to python3 http.server.
        _serverProcess = StartStaticServer(wwwrootPath, _serverPort);
        _ = CaptureProcessOutputAsync(_serverProcess, _processLogPath);

        // 5. Wait for server to be ready (fail fast if process dies).
        await WaitForServerReady($"http://localhost:{_serverPort}/", _serverProcess, _processLogPath, timeoutMs: 15_000);

        // 6. Launch Playwright Chromium browser (headless).
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
        });

        // 7. Create context and page.
        Context = await _browser.NewContextAsync();

        // Start tracing for failure artifact collection.
        await Context.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
        });

        Page = await Context.NewPageAsync();

        // 8. Start capturing console output BEFORE navigating. C# Console.WriteLine surfaces
        // here under WASM, and the readiness marker is emitted during boot -- subscribing after
        // GotoAsync would race it.
        Page.Console += OnPageConsole;

        // 9. Navigate to the WASM app.
        await Page.GotoAsync($"http://localhost:{_serverPort}/", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = MonacoReadyTimeoutMs,
        });

        // 10. Wait for a Monaco instance to exist.
        await Page.WaitForFunctionAsync(
            "() => typeof monaco !== 'undefined' && monaco.editor.getEditors().length > 0",
            null, new PageWaitForFunctionOptions { Timeout = MonacoReadyTimeoutMs });

        // 11. Wait for the app to finish its own initialisation.
        //
        // The step above is NOT sufficient on its own: it goes green the moment the editor is
        // constructed, while EditorControl.Editor_Loading is still fetching Content.txt and
        // pushing it into the model. Tests that start in that window get their writes clobbered
        // by the late push -- observed on CI run 32854772712, where setValue landed at t+9.13s
        // and the following getValue at t+9.29s returned Content.txt instead.
        await WaitForAppInitCompleteAsync();

        // 12. Snapshot the settled content for tests that assert on first-load state.
        InitialEditorText = await Page.EvaluateAsync<string>(
            "() => monaco.editor.getEditors()[0].getValue()");
    }

    private void OnPageConsole(object? sender, IConsoleMessage message)
    {
        var text = message.Text ?? string.Empty;

        lock (_consoleLock)
        {
            _consoleLines.Add($"[{message.Type}] {text}");
        }

        if (text.Contains(AppInitSettledMarker, StringComparison.Ordinal))
        {
            _appInitComplete.TrySetResult(text);
        }
    }

    /// <summary>
    /// Blocks until the app emits its init-complete marker, failing with the captured console
    /// output rather than a bare timeout when it never arrives.
    /// </summary>
    private async Task WaitForAppInitCompleteAsync()
    {
        using var timeoutCts = new CancellationTokenSource();

        var completed = await Task.WhenAny(
            _appInitComplete.Task,
            Task.Delay(MonacoReadyTimeoutMs, timeoutCts.Token));

        // Release the timer as soon as the marker wins, rather than leaving it armed for the
        // rest of the timeout on every run.
        await timeoutCts.CancelAsync();

        if (completed != _appInitComplete.Task)
        {
            throw new TimeoutException(
                $"The app did not emit '{AppInitSettledMarker}' within {MonacoReadyTimeoutMs}ms. " +
                "Tests cannot start before it without racing the app's initial text push.\n" +
                "Console output so far:\n  " + string.Join("\n  ", GetConsoleLines()));
        }

        var marker = await _appInitComplete.Task;

        if (marker.Contains($"{AppInitSettledMarker}:error=", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The app reported a failure while initialising: {marker}");
        }
    }

    /// <summary>Console output captured from the page since navigation.</summary>
    public IReadOnlyList<string> GetConsoleLines()
    {
        lock (_consoleLock)
        {
            return [.. _consoleLines];
        }
    }

    /// <summary>
    /// Returns the editor to a known state so tests in the shared collection do not inherit each
    /// other's mutations. Mirrors <c>DesktopAppFixture.ResetEditorStateAsync</c>.
    /// </summary>
    public async Task ResetEditorStateAsync()
    {
        await Page.EvaluateAsync($$"""
            () => {
                const editor = monaco.editor.getEditors()[0];
                editor.setValue('{{ResetText}}');
                const model = editor.getModel();
                if (model) {
                    monaco.editor.setModelLanguage(model, 'javascript');
                    monaco.editor.setModelMarkers(model, 'test', []);
                    monaco.editor.setModelMarkers(model, 'CodeEditor', []);
                    editor.deltaDecorations(
                        model.getAllDecorations().map(d => d.id),
                        []
                    );
                }
                monaco.editor.setTheme('vs');
                editor.updateOptions({ readOnly: false });
            }
            """);
    }

    public async ValueTask DisposeAsync()
    {
        if (Page is not null)
        {
            Page.Console -= OnPageConsole;
        }

        if (_browser is not null)
        {
            try { await _browser.CloseAsync(); } catch { /* best-effort */ }
        }

        if (_serverProcess is { HasExited: false })
        {
            try
            {
                _serverProcess.Kill(entireProcessTree: true);
                await _serverProcess.WaitForExitAsync(new CancellationTokenSource(5000).Token);
            }
            catch { /* best-effort */ }
        }

        _serverProcess?.Dispose();
        _playwright?.Dispose();
    }

    /// <summary>
    /// Captures a failure screenshot and stops the Playwright trace, saving artifacts
    /// to the <c>test-artifacts/</c> directory.
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

        if (File.Exists(_processLogPath))
        {
            try
            {
                File.Copy(_processLogPath, Path.Combine(artifactsDir, $"{testName}-process.log"), overwrite: true);
            }
            catch { /* best-effort */ }
        }

        try
        {
            // The page console carries the app's own markers, so a failure here can be read
            // against what the app had actually done by that point.
            await File.WriteAllLinesAsync(
                Path.Combine(artifactsDir, $"{testName}-console.log"),
                GetConsoleLines());
        }
        catch { /* best-effort */ }
    }

    private static string ResolveWasmBuildOutput(string repoRoot)
    {
        // Search candidates in priority order: artifacts output (UseArtifactsOutput=true),
        // then traditional bin layout, for both Release and Debug.
        string[] candidates =
        [
            Path.Combine(repoRoot, "artifacts", "bin", "MonacoEditorTestApp", "release_net10.0-browserwasm", "wwwroot"),
            Path.Combine(repoRoot, "MonacoEditorTestApp", "bin", "Release", "net10.0-browserwasm", "wwwroot"),
            Path.Combine(repoRoot, "artifacts", "bin", "MonacoEditorTestApp", "debug_net10.0-browserwasm", "wwwroot"),
            Path.Combine(repoRoot, "MonacoEditorTestApp", "bin", "Debug", "net10.0-browserwasm", "wwwroot"),
        ];

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "WASM build output not found. Run:\n" +
            "  dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm\n" +
            $"Searched:\n  {string.Join("\n  ", candidates)}");
    }

    private static Process StartStaticServer(string wwwrootPath, int port)
    {
        // Try multiple static file servers in priority order for cross-platform support.
        // dotnet-serve is preferred (.NET ecosystem), then python3 (CI images), then python/py (Windows).
        (string fileName, string arguments)[] candidates =
        [
            ("dotnet", $"serve --port {port} --directory \"{wwwrootPath}\""),
            ("python3", $"-m http.server {port} --directory \"{wwwrootPath}\""),
            ("python", $"-m http.server {port} --directory \"{wwwrootPath}\""),
            ("py", $"-3 -m http.server {port} --directory \"{wwwrootPath}\""),
        ];

        var attempted = new List<string>();

        foreach (var (fileName, arguments) in candidates)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                var process = Process.Start(startInfo);
                if (process is null)
                {
                    attempted.Add($"{fileName} (returned null)");
                    continue;
                }

                // Wait briefly to verify the process survives startup.
                // If dotnet-serve is not installed, `dotnet serve` starts then exits
                // immediately -- we need to detect that and try the next candidate.
                Thread.Sleep(500);
                if (process.HasExited)
                {
                    var exitCode = process.ExitCode;
                    process.Dispose();
                    attempted.Add($"{fileName} (exited immediately with code {exitCode})");
                    continue;
                }

                return process;
            }
            catch (Exception ex)
            {
                attempted.Add($"{fileName} ({ex.GetType().Name}: {ex.Message})");
            }
        }

        throw new InvalidOperationException(
            "Failed to start static file server. Tried:\n" +
            string.Join("\n", attempted.Select(a => $"  - {a}")));
    }

    private static async Task WaitForServerReady(string url, Process serverProcess, string logPath, int timeoutMs)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            // Fail fast if the server process has died.
            if (serverProcess.HasExited)
            {
                var exitCode = serverProcess.ExitCode;
                var logContent = File.Exists(logPath) ? File.ReadAllText(logPath) : "(no log)";
                throw new InvalidOperationException(
                    $"Static file server process exited unexpectedly with code {exitCode} before becoming ready.\n" +
                    $"Process log:\n{logContent}");
            }

            try
            {
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }

            await Task.Delay(500);
        }

        var logOnTimeout = File.Exists(logPath) ? File.ReadAllText(logPath) : "(no log)";
        throw new TimeoutException(
            $"Static file server at {url} did not become ready within {timeoutMs}ms.\n" +
            $"Process log:\n{logOnTimeout}");
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

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private static async Task CaptureProcessOutputAsync(Process process, string logPath)
    {
        try
        {
            await using var logWriter = new StreamWriter(logPath, append: false);
            // ReadLineAsync returns null at end-of-stream, avoiding the CA2024
            // diagnostic from synchronous EndOfStream checks in async methods.
            var stdoutTask = Task.Run(async () =>
            {
                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
                {
                    await logWriter.WriteLineAsync($"[stdout] {line}");
                    await logWriter.FlushAsync();
                }
            });
            var stderrTask = Task.Run(async () =>
            {
                string? line;
                while ((line = await process.StandardError.ReadLineAsync()) is not null)
                {
                    await logWriter.WriteLineAsync($"[stderr] {line}");
                    await logWriter.FlushAsync();
                }
            });

            await Task.WhenAny(Task.WhenAll(stdoutTask, stderrTask), Task.Delay(TimeSpan.FromMinutes(5)));
        }
        catch { /* best-effort */ }
    }
}
