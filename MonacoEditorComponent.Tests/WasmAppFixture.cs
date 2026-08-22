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

    private IPlaywright? _playwright;
    private Process? _serverProcess;
    private IBrowser? _browser;
    private string _processLogPath = string.Empty;
    private int _serverPort;

    /// <summary>The Playwright page connected to the WASM app.</summary>
    public IPage Page { get; private set; } = null!;

    /// <summary>The Playwright browser context for tracing support.</summary>
    public IBrowserContext Context { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var repoRoot = FindRepoRoot();

        // 0. Create Playwright instance (owned by this fixture).
        PlaywrightDriverPath.Ensure();

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

        // 8. Navigate to the WASM app.
        await Page.GotoAsync($"http://localhost:{_serverPort}/", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = MonacoReadyTimeoutMs,
        });

        // 9. Wait for Monaco to be ready.
        await Page.WaitForFunctionAsync(
            "() => typeof monaco !== 'undefined' && monaco.editor.getEditors().length > 0",
            null, new PageWaitForFunctionOptions { Timeout = MonacoReadyTimeoutMs });
    }

    public async ValueTask DisposeAsync()
    {
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
