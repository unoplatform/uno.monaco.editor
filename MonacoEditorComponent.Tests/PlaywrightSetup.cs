using Microsoft.Playwright;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Provides a shared <see cref="IPlaywright"/> instance for all integration tests.
/// xUnit v3 <c>AssemblyFixture</c> ensures exactly one instance per test run.
/// </summary>
/// <remarks>
/// Playwright browser install is handled externally (CI step or local dev setup):
/// <code>
/// pwsh bin/Debug/net10.0/playwright.ps1 install chromium
/// </code>
/// The NuGet package's build targets are excluded in the csproj to avoid
/// OutputType=Exe path conflicts on macOS/Linux. See the pitfall note in memory.
/// </remarks>
public sealed class PlaywrightSetup : IAsyncLifetime
{
    public IPlaywright Instance { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Instance = await Playwright.CreateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        Instance.Dispose();
        await ValueTask.CompletedTask;
    }
}
