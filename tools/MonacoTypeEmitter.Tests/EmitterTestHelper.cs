#nullable enable

using System.Text.Json;
using MonacoTypeEmitter.Emitter;
using MonacoTypeEmitter.Model;

namespace MonacoTypeEmitter.Tests;

/// <summary>
/// Helper to run the CSharpEmitter against test inputs and capture file output.
/// </summary>
internal static class EmitterTestHelper
{
    /// <summary>
    /// Loads a MonacoModel from a JSON file path.
    /// </summary>
    public static MonacoModel LoadModel(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        return JsonSerializer.Deserialize<MonacoModel>(json)
            ?? throw new InvalidOperationException($"Failed to deserialize model from {jsonPath}");
    }

    /// <summary>
    /// Loads a MonacoModel from a JSON string.
    /// </summary>
    public static MonacoModel LoadModelFromJson(string json)
    {
        return JsonSerializer.Deserialize<MonacoModel>(json)
            ?? throw new InvalidOperationException("Failed to deserialize model from JSON string");
    }

    /// <summary>
    /// Runs the emitter against the given model and returns a dictionary of
    /// relative path -> file content for all emitted files.
    /// </summary>
    public static Dictionary<string, string> EmitToMemory(MonacoModel model, IgnoreList? ignoreList = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"emitter-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            ignoreList ??= IgnoreList.Load(Path.Combine(tempDir, "nonexistent-ignore-file"));
            var emitter = new CSharpEmitter(model, ignoreList, tempDir, tempDir);
            emitter.EmitAll();

            // Read all generated files back
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(tempDir, "*.cs", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(tempDir, file).Replace('\\', '/');
                result[relativePath] = File.ReadAllText(file);
            }

            return result;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch { /* best effort cleanup */ }
        }
    }

    /// <summary>
    /// Returns the path to a test input JSON file in the Snapshots/TestInputs directory.
    /// </summary>
    public static string GetTestInputPath(string fileName)
    {
        // Walk up from the test assembly to find the project directory
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "Snapshots", "TestInputs", fileName);
            if (File.Exists(candidate))
                return candidate;

            // Also check the project directory (for source-relative paths)
            var projDir = Path.Combine(dir, "tools", "MonacoTypeEmitter.Tests", "Snapshots", "TestInputs", fileName);
            if (File.Exists(projDir))
                return projDir;

            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException($"Test input file not found: {fileName}");
    }

    /// <summary>
    /// Returns the path to the full model.json from the extractor output.
    /// </summary>
    public static string GetFullModelPath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "tools", "monaco-type-extractor", "output", "model.json");
            if (File.Exists(candidate))
                return candidate;

            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException("Full model.json not found in monaco-type-extractor/output/");
    }
}
