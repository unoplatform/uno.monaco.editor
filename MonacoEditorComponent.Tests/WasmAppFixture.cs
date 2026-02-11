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

    private readonly PlaywrightSetup _playwrightSetup;
    private Process? _serverProcess;
    private IBrowser? _browser;
    private string _processLogPath = string.Empty;
    private int _serverPort;

    /// <summary>The Playwright page connected to the WASM app.</summary>
    public IPage Page { get; private set; } = null!;

    /// <summary>The Playwright browser context for tracing support.</summary>
    public IBrowserContext Context { get; private set; } = null!;

    public WasmAppFixture(PlaywrightSetup playwrightSetup)
    {
        _playwrightSetup = playwrightSetup;
    }

    public async ValueTask InitializeAsync()
    {
        var repoRoot = FindRepoRoot();

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

        // 5. Wait for server to be ready.
        await WaitForServerReady($"http://localhost:{_serverPort}/", timeoutMs: 15_000);

        // 6. Launch Playwright Chromium browser (headless).
        _browser = await _playwrightSetup.Instance.Chromium.LaunchAsync(new()
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
        var releasePath = Path.Combine(repoRoot, "MonacoEditorTestApp", "bin", "Release", "net10.0-browserwasm", "wwwroot");
        if (Directory.Exists(releasePath))
        {
            return releasePath;
        }

        var debugPath = Path.Combine(repoRoot, "MonacoEditorTestApp", "bin", "Debug", "net10.0-browserwasm", "wwwroot");
        if (Directory.Exists(debugPath))
        {
            return debugPath;
        }

        throw new InvalidOperationException(
            "WASM build output not found. Run:\n" +
            "  dotnet build MonacoEditorTestApp/MonacoEditorTestApp.csproj -f net10.0-browserwasm\n" +
            $"Searched:\n  {releasePath}\n  {debugPath}");
    }

    private static Process StartStaticServer(string wwwrootPath, int port)
    {
        // Use python3 http.server as a cross-platform static file server.
        // It is available on all CI images and developer machines.
        var startInfo = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"-m http.server {port} --directory \"{wwwrootPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Failed to start static file server. Ensure python3 is on PATH.");
    }

    private static async Task WaitForServerReady(string url, int timeoutMs)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
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

        throw new TimeoutException(
            $"Static file server at {url} did not become ready within {timeoutMs}ms.");
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
