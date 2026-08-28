using System.Reflection;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Points Playwright at the driver bundled in the Microsoft.Playwright NuGet package
/// (the <c>.playwright/</c> folder).
/// </summary>
/// <remarks>
/// The test project excludes Playwright's build assets (see the .csproj) because they
/// misbehave under <c>UseArtifactsOutput</c>, so the driver is not copied next to the test
/// DLL and <c>Playwright.CreateAsync()</c> fails with "Driver not found". CI sets
/// <c>PLAYWRIGHT_DRIVER_SEARCH_PATH</c> explicitly; this makes local runs work without manual
/// setup, and every fixture that creates a Playwright instance must call it first.
/// </remarks>
internal static class PlaywrightDriverPath
{
    private const string EnvVar = "PLAYWRIGHT_DRIVER_SEARCH_PATH";

    /// <summary>
    /// Sets <c>PLAYWRIGHT_DRIVER_SEARCH_PATH</c> from the package path baked in at build time.
    /// No-op when the variable is already set (for example on CI) or the path is unavailable.
    /// </summary>
    public static void Ensure()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVar)))
        {
            return;
        }

        var packagePath = typeof(PlaywrightDriverPath).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "PlaywrightPackagePath")?.Value;

        if (!string.IsNullOrEmpty(packagePath) && Directory.Exists(packagePath))
        {
            Environment.SetEnvironmentVariable(EnvVar, packagePath);
        }
    }
}
