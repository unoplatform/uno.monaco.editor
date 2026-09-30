using System.Diagnostics;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Tests for <see cref="WasmAppFixture.StartStaticServerAsync"/>, the candidate fall-through that
/// picks whichever static file server this machine actually has.
///
/// <para>Regression cover for CI run 33770173883: the previous implementation accepted any
/// candidate still running 500ms after <c>Process.Start</c> and only then probed it, so a
/// candidate that was not a working server could never be fallen through from. When the dotnet
/// muxer took longer than that grace period to report the missing <c>dotnet-serve</c> tool and
/// exit 1, the doomed process was returned as the winner and every WASM integration test failed
/// without python ever being tried.</para>
///
/// <para>Budgets here are generous on purpose: a cold python takes over 5s to answer on the
/// Windows runner, so a candidate is only ever rejected for dying, never for being slower than a
/// number picked on a fast dev box.</para>
///
/// <para>The two tests that need a real server carry <c>Category=StaticServer</c>, which the
/// macOS ARM job excludes. A python http.server there accepts the connection and then never
/// answers -- it prints nothing even unbuffered, and probes time out rather than being refused,
/// so it hangs somewhere before it serves. That is the same behaviour behind this repo keeping
/// the WASM suite off that runner, and worth its own investigation rather than a bigger
/// number here.</para>
///
/// <para>These tests need no browser and no prebuilt WASM app, so they run in every CI job
/// rather than only where the Playwright suite does.</para>
/// </summary>
public sealed class StaticServerFallbackTests : IDisposable
{
    /// <summary>A dotnet verb that cannot exist, so the muxer always prints and exits 1.</summary>
    private const string MissingDotnetVerb = "serve-no-such-command";

    /// <summary>
    /// How long the deliberately useless candidate lives. Long enough to still be running when
    /// the first probe hits it -- the old code accepted anything alive after 500ms -- and short
    /// enough that rejecting it costs the suite seconds rather than a whole timeout budget.
    /// </summary>
    private const int DoomedCandidateLifetimeSeconds = 3;

    /// <summary>
    /// The python this machine has, resolved once. Every CI runner ships one; a dev box without
    /// python skips the tests that need a real server rather than failing.
    /// </summary>
    private static readonly (string FileName, string ArgumentPrefix)? Python = ResolvePython();

    private readonly string _rootDirectory;

    public StaticServerFallbackTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"monaco-static-server-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(_rootDirectory, recursive: true); }
        catch (IOException) { /* temp folder still held by the OS; not a test failure */ }
        catch (UnauthorizedAccessException) { /* cleanup denied by ACLs; not a test failure */ }
    }

    [Fact]
    [Trait("Category", "StaticServer")]
    public async Task StartStaticServerAsync_SkipsCandidatesItCannotEvenStart()
    {
        Assert.SkipWhen(Python is null, "No python available to serve the fallback candidate.");

        var handle = await StartAsync(
            [
                // Process.Start itself fails: nothing by this name is on PATH.
                new($"no-such-server-{Guid.NewGuid():N}", (_, _) => string.Empty),
                // Starts, prints "Could not execute because the specified command or file was not
                // found", exits 1 -- the shape that broke CI.
                new("dotnet", (root, port) => $"{MissingDotnetVerb} --port {port} --directory \"{root}\""),
                PythonServerCandidate(),
            ]);

        await AssertServesProbeFileAsync(handle);
    }

    [Fact]
    [Trait("Category", "StaticServer")]
    public async Task StartStaticServerAsync_FallsThroughACandidateThatOutlivesItsStartup()
    {
        Assert.SkipWhen(Python is null, "No python available to serve the fallback candidate.");

        // A process that is alive for the first probes and never binds the port. Surviving
        // startup is not evidence of a working server, which is exactly what the old 500ms
        // liveness check assumed: it would have returned this sleeper and then failed the run on
        // a readiness timeout, with the working candidate behind it never tried.
        var handle = await StartAsync(
            [
                new(Python!.Value.FileName,
                    (_, _) => PythonArguments(
                        $"-c \"import time; time.sleep({DoomedCandidateLifetimeSeconds})\"")),
                PythonServerCandidate(),
            ]);

        await AssertServesProbeFileAsync(handle);
    }

    [Fact]
    public async Task StartStaticServerAsync_ReportsWhyEveryCandidateWasRejected()
    {
        WasmAppFixture.StaticServerCandidate[] candidates =
        [
            new("dotnet", (root, port) => $"{MissingDotnetVerb} --port {port} --directory \"{root}\""),
        ];

        var elapsed = Stopwatch.StartNew();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await WasmAppFixture.StartStaticServerAsync(
                _rootDirectory, _rootDirectory, candidates, perCandidateTimeoutMs: 30_000));

        elapsed.Stop();

        Assert.Contains("dotnet", ex.Message);

        // The candidate's death ends the attempt, so the budget is never spent. Waiting it out
        // instead would still fail the run, only 30s later and with a timeout for a diagnosis.
        Assert.True(
            elapsed.Elapsed < TimeSpan.FromSeconds(20),
            $"rejecting a dead candidate took {elapsed.Elapsed}, which means the timeout was waited out");
    }

    /// <summary>
    /// Runs the candidate list against the temp root, writing the probe file the assertions below
    /// look for and keeping process logs out of <c>test-artifacts/</c>.
    /// </summary>
    private async Task<WasmAppFixture.StaticServerHandle> StartAsync(
        IReadOnlyList<WasmAppFixture.StaticServerCandidate> candidates,
        int perCandidateTimeoutMs = 30_000)
    {
        await File.WriteAllTextAsync(
            Path.Combine(_rootDirectory, "index.html"), "<html>static-server-probe</html>");

        return await WasmAppFixture.StartStaticServerAsync(
            _rootDirectory, _rootDirectory, candidates, perCandidateTimeoutMs);
    }

    /// <summary>Asserts the handle is a live server actually serving the temp root.</summary>
    private static async Task AssertServesProbeFileAsync(WasmAppFixture.StaticServerHandle handle)
    {
        try
        {
            Assert.False(handle.Process.HasExited, "the returned server had already exited");

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var body = await client.GetStringAsync($"http://localhost:{handle.Port}/index.html");

            Assert.Contains("static-server-probe", body);
        }
        finally
        {
            try { handle.Process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            handle.Process.Dispose();
        }
    }

    private static WasmAppFixture.StaticServerCandidate PythonServerCandidate() =>
        new(Python!.Value.FileName,
            (root, port) => PythonArguments($"-m http.server {port} --directory \"{root}\""));

    /// <summary>Prefixes python arguments with the launcher's version selector when needed.</summary>
    private static string PythonArguments(string arguments) => Python!.Value.ArgumentPrefix + arguments;

    /// <summary>
    /// Finds a python that actually runs. The Windows Store stub named <c>python3</c> is on PATH
    /// on machines without python installed and exits non-zero, so each candidate is executed
    /// rather than merely resolved.
    /// </summary>
    private static (string, string)? ResolvePython()
    {
        (string FileName, string ArgumentPrefix)[] candidates =
        [
            ("python3", string.Empty),
            ("python", string.Empty),
            ("py", "-3 "),
        ];

        foreach (var (fileName, argumentPrefix) in candidates)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = argumentPrefix + "-c pass",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });

                if (process is null)
                {
                    continue;
                }

                if (!process.WaitForExit(15_000))
                {
                    process.Kill(entireProcessTree: true);
                    continue;
                }

                if (process.ExitCode == 0)
                {
                    return (fileName, argumentPrefix);
                }
            }
            catch
            {
                // Not on PATH, or not executable here; try the next spelling.
            }
        }

        return null;
    }
}
