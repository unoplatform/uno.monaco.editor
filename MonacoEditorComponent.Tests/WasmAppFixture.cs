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
        PlaywrightDriverPath.Ensure();

        _playwright = await Playwright.CreateAsync();

        // 1. Resolve WASM build output directory (Release then Debug fallback).
        var wwwrootPath = ResolveWasmBuildOutput(repoRoot);

        // 2. Ensure the test-artifacts directory exists -- every server candidate logs into it.
        var artifactsDir = Path.Combine(repoRoot, "test-artifacts");
        Directory.CreateDirectory(artifactsDir);

        // 3. Start a static file server on the wwwroot path. Candidates are probed to readiness
        // one at a time and the first that answers wins, so a command that is not installed
        // costs an attempt rather than the whole run.
        var server = await StartStaticServerAsync(wwwrootPath, artifactsDir, DefaultStaticServerCandidates);
        _serverProcess = server.Process;
        _serverPort = server.Port;
        _processLogPath = server.LogPath;

        // 4. Launch Playwright Chromium browser (headless).
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
        });

        // 5. Create context and page.
        Context = await _browser.NewContextAsync();

        // Start tracing for failure artifact collection.
        await Context.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
        });

        Page = await Context.NewPageAsync();

        // 6. Start capturing console output BEFORE navigating. C# Console.WriteLine surfaces
        // here under WASM, and the readiness marker is emitted during boot -- subscribing after
        // GotoAsync would race it.
        Page.Console += OnPageConsole;

        // 7. Navigate to the WASM app.
        await Page.GotoAsync($"http://localhost:{_serverPort}/", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = MonacoReadyTimeoutMs,
        });

        // 8. Wait for the sample's own editor to exist. Counted through the standalone-editor
        // filter, not getEditors(): WASM runs both samples in ONE page against ONE monaco
        // instance, so getEditors() also lists the diff widget's two sub-editors and would go
        // green on a page where the plain editor had not been created at all.
        await Page.WaitForFunctionAsync(
            $"() => typeof monaco !== 'undefined' && {DiffEditorCases.StandaloneEditorsExpressionBody}.length > 0",
            null, new PageWaitForFunctionOptions { Timeout = MonacoReadyTimeoutMs });

        // 9. Wait for the app to finish its own initialisation.
        //
        // The step above is NOT sufficient on its own: it goes green the moment the editor is
        // constructed, while EditorControl.Editor_Loading is still fetching Content.txt and
        // pushing it into the model. Tests that start in that window get their writes clobbered
        // by the late push -- observed on CI run 32854772712, where setValue landed at t+9.13s
        // and the following getValue at t+9.29s returned Content.txt instead.
        await WaitForAppInitCompleteAsync();

        // 10. Snapshot the settled content for tests that assert on first-load state. Indexing
        // getEditors() here would read whichever editor happened to be constructed first, which
        // on this page may be a diff sub-editor holding the diff sample's document instead.
        InitialEditorText = await Page.EvaluateAsync<string>(
            $"() => {DiffEditorCases.StandaloneEditorsExpressionBody}[0].getValue()");
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
        // The editor is selected through the standalone filter for the same reason the
        // snapshot above is: resetting a diff sub-editor would leave the sample editor holding
        // the previous test's mutations while wiping a document no test owns.
        await Page.EvaluateAsync($$"""
            () => {
                const editor = {{DiffEditorCases.StandaloneEditorsExpressionBody}}[0];
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

        if (_serverProcess is not null)
        {
            await TerminateServerAsync(_serverProcess);
            _serverProcess.Dispose();
        }

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

    /// <summary>
    /// One static-file-server command to try. <c>BuildArguments</c> receives the directory to
    /// serve and the port to bind.
    /// </summary>
    internal readonly record struct StaticServerCandidate(
        string FileName,
        Func<string, int, string> BuildArguments);

    /// <summary>A started static file server that has already answered on its port.</summary>
    internal sealed record StaticServerHandle(Process Process, int Port, string LogPath);

    /// <summary>
    /// Static file servers to try, in priority order. dotnet-serve is preferred (.NET ecosystem),
    /// then python3 (CI images), then python/py (Windows). None of them is guaranteed to exist --
    /// nothing in this repo installs dotnet-serve, so on CI the first candidate always fails and
    /// the fall-through below is the load-bearing path, not a nicety.
    /// </summary>
    internal static IReadOnlyList<StaticServerCandidate> DefaultStaticServerCandidates { get; } =
    [
        new("dotnet", (root, port) => $"serve --port {port} --directory \"{root}\""),
        new("python3", (root, port) => $"-m http.server {port} --directory \"{root}\""),
        new("python", (root, port) => $"-m http.server {port} --directory \"{root}\""),
        new("py", (root, port) => $"-3 -m http.server {port} --directory \"{root}\""),
    ];

    /// <summary>
    /// Starts the first candidate command that actually serves <paramref name="wwwrootPath"/>.
    ///
    /// <para>Each candidate is probed all the way to an HTTP response before it is accepted, and
    /// rejected the moment its process exits. An earlier version instead slept 500ms and accepted
    /// whatever was still running, which made "is this tool installed?" a race against the dotnet
    /// muxer's startup: on CI run 33770173883 the dotnet candidate took longer than that to print
    /// "Could not execute because the specified command or file was not found" and exit 1, so the
    /// doomed process was returned as the winner and all 11 WASM tests failed without python ever
    /// being tried.</para>
    /// </summary>
    internal static async Task<StaticServerHandle> StartStaticServerAsync(
        string wwwrootPath,
        string logDirectory,
        IReadOnlyList<StaticServerCandidate> candidates,
        int perCandidateTimeoutMs = 15_000)
    {
        var attempted = new List<string>();

        foreach (var candidate in candidates)
        {
            // A fresh port per attempt: the candidate being abandoned may have bound the previous
            // one, and racing its teardown for the same port buys nothing.
            var port = GetAvailablePort();
            var logPath = Path.Combine(
                logDirectory,
                $"wasm-fixture-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{candidate.FileName}.log");

            Process process;
            try
            {
                var started = Process.Start(new ProcessStartInfo
                {
                    FileName = candidate.FileName,
                    Arguments = candidate.BuildArguments(wwwrootPath, port),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });

                if (started is null)
                {
                    attempted.Add($"{candidate.FileName} (Process.Start returned null)");
                    continue;
                }

                process = started;
            }
            catch (Exception ex)
            {
                attempted.Add($"{candidate.FileName} ({ex.GetType().Name}: {ex.Message})");
                continue;
            }

            // Capture output for every candidate, not just the winner: a rejected candidate's
            // stderr is the only thing that says why it was rejected.
            _ = CaptureProcessOutputAsync(process, logPath);

            if (await WaitForServerReadyAsync($"http://localhost:{port}/", process, perCandidateTimeoutMs))
            {
                return new StaticServerHandle(process, port, logPath);
            }

            attempted.Add($"{candidate.FileName} ({await DescribeRejectionAsync(process, logPath)})");
            await TerminateServerAsync(process);
            process.Dispose();
        }

        throw new InvalidOperationException(
            "Failed to start a static file server for the WASM app. Tried:\n" +
            string.Join("\n", attempted.Select(a => $"  - {a}")));
    }

    /// <summary>
    /// Polls <paramref name="url"/> until it answers, returning false as soon as the server
    /// process dies or the budget runs out. Process death is the deterministic "this command is
    /// unusable" signal, so it is checked every pass rather than once after a fixed sleep.
    /// </summary>
    private static async Task<bool> WaitForServerReadyAsync(string url, Process serverProcess, int timeoutMs)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (serverProcess.HasExited)
            {
                return false;
            }

            try
            {
                using var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }

            await Task.Delay(250);
        }

        return false;
    }

    /// <summary>Explains why a candidate was rejected, quoting whatever it managed to print.</summary>
    private static async Task<string> DescribeRejectionAsync(Process process, string logPath)
    {
        var state = process.HasExited
            ? $"exited with code {process.ExitCode}"
            : "never answered on its port";

        // The output capture writes from its own task, so give the last lines a moment to land
        // before quoting them -- this runs only on the failure path.
        await Task.Delay(200);

        var log = ReadLogTail(logPath);
        return log.Length == 0 ? state : $"{state}; output: {log}";
    }

    /// <summary>
    /// Reads the tail of a process log that is still open for writing, hence the explicit
    /// <see cref="FileShare.ReadWrite"/> rather than <c>File.ReadAllText</c>.
    /// </summary>
    private static string ReadLogTail(string logPath, int maxLines = 20)
    {
        try
        {
            using var stream = new FileStream(
                logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);

            var lines = reader.ReadToEnd()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0);

            return string.Join(" | ", lines.TakeLast(maxLines));
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Kills a server process and its children, best-effort.</summary>
    private static async Task TerminateServerAsync(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            // entireProcessTree: the dotnet muxer and the py launcher both run the real server as
            // a child, which would outlive a kill aimed at the parent alone.
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(new CancellationTokenSource(5000).Token);
        }
        catch { /* best-effort */ }
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
