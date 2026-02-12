#nullable enable

using Xunit;

namespace MonacoTypeEmitter.Tests;

/// <summary>
/// Compares actual emitter output against checked-in .verified.cs baseline files.
/// If a baseline does not exist, it creates a .received.cs file for review and fails.
/// </summary>
internal static class SnapshotAssert
{
    /// <summary>
    /// Asserts that the actual content matches the verified baseline for the given test name.
    /// Baselines are stored in Snapshots/Verified/{testName}.verified.cs.
    /// On mismatch, a .received.cs file is written for comparison.
    /// </summary>
    public static void MatchesVerified(string testName, string actual)
    {
        var verifiedDir = GetVerifiedDirectory();
        var verifiedPath = Path.Combine(verifiedDir, $"{testName}.verified.cs");
        var receivedPath = Path.Combine(verifiedDir, $"{testName}.received.cs");

        // Normalize line endings for cross-platform comparison
        actual = NormalizeLineEndings(actual);

        if (!File.Exists(verifiedPath))
        {
            // Write .received.cs for the user to review and promote
            File.WriteAllText(receivedPath, actual);
            Assert.Fail(
                $"No verified baseline found at: {verifiedPath}\n" +
                $"Received output written to: {receivedPath}\n" +
                $"Review and rename to .verified.cs to accept.");
        }

        var expected = NormalizeLineEndings(File.ReadAllText(verifiedPath));

        if (expected != actual)
        {
            File.WriteAllText(receivedPath, actual);
            Assert.Fail(
                $"Snapshot mismatch for '{testName}'.\n" +
                $"Expected (verified): {verifiedPath}\n" +
                $"Actual (received):   {receivedPath}\n" +
                $"Compare the two files and update the .verified.cs if the change is intentional.");
        }

        // Clean up any stale .received.cs on success
        if (File.Exists(receivedPath))
            File.Delete(receivedPath);
    }

    private static string GetVerifiedDirectory()
    {
        // Walk up from test assembly base to find Snapshots/Verified.
        // Prefer the project source directory over the build output directory
        // so that .received.cs files land next to .verified.cs baselines for
        // easy promotion (rename .received.cs -> .verified.cs).
        var dir = AppContext.BaseDirectory;
        string? outputCandidate = null;

        while (dir is not null)
        {
            // Project source directory (preferred -- enables checked-in snapshot workflow)
            var projCandidate = Path.Combine(dir, "tools", "MonacoTypeEmitter.Tests", "Snapshots", "Verified");
            if (Directory.Exists(projCandidate))
                return projCandidate;

            // Build output directory (fallback only)
            if (outputCandidate is null)
            {
                var candidate = Path.Combine(dir, "Snapshots", "Verified");
                if (Directory.Exists(candidate))
                    outputCandidate = candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        // Fall back to build output if source directory not found
        if (outputCandidate is not null)
            return outputCandidate;

        throw new DirectoryNotFoundException(
            "Could not find Snapshots/Verified directory. Ensure it exists in the test project.");
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").TrimEnd();
}
