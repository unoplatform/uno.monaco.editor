#nullable enable

using System.Diagnostics;
using System.Text.Json;
using MonacoTypeEmitter.Model;
using Xunit;

namespace MonacoTypeEmitter.Tests;

/// <summary>
/// Smoke tests that run the full pipeline: parse real monaco.d.ts -> intermediate JSON ->
/// emit C# -> validate structure and compilation of emitted files.
/// </summary>
[Trait("Category", "Smoke")]
public class SmokeTests
{
    /// <summary>
    /// Full pipeline smoke test: load real model.json from extractor output,
    /// emit C# files, then compile the enum subset (which has no external dependencies
    /// and no edge-case identifier issues) in an isolated temp project.
    /// This validates the full parse -> emit -> compile pipeline.
    /// </summary>
    [Fact]
    public void FullPipeline_EmitAndCompileEnums()
    {
        var modelPath = EmitterTestHelper.GetFullModelPath();
        var model = EmitterTestHelper.LoadModel(modelPath);

        // Verify model loaded correctly
        Assert.True(model.SchemaVersion >= 1, "Schema version must be >= 1");
        Assert.NotEmpty(model.Namespaces);

        // Emit to a temp directory
        var tempDir = Path.Combine(Path.GetTempPath(), $"smoke-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = Path.Combine(tempDir, "Monaco");
            Directory.CreateDirectory(outputDir);

            var ignoreList = MonacoTypeEmitter.Emitter.IgnoreList.Load(
                Path.Combine(tempDir, "nonexistent"));
            var emitter = new MonacoTypeEmitter.Emitter.CSharpEmitter(
                model, ignoreList, outputDir, tempDir);
            var written = emitter.EmitAll();

            // Must emit a meaningful number of files
            Assert.True(written.Count > 50,
                $"Expected >50 emitted files from full model, got {written.Count}");

            // Verify key files were emitted
            var fileNames = written.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("MarkerSeverity.cs", fileNames);
            Assert.Contains("CompletionItemKind.cs", fileNames);

            // Create an isolated project that compiles only enum files
            // (these are self-contained, no cross-type dependencies, and validate
            // the full pipeline: JSON model -> C# emission -> valid compilation)
            var projDir = Path.Combine(tempDir, "CompileTest");
            Directory.CreateDirectory(projDir);

            // Copy only enum files (files that contain "public enum")
            var enumDir = Path.Combine(projDir, "Enums");
            Directory.CreateDirectory(enumDir);
            int enumCount = 0;

            foreach (var file in Directory.EnumerateFiles(outputDir, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                if (content.Contains("public enum "))
                {
                    File.Copy(file, Path.Combine(enumDir, Path.GetFileName(file)), overwrite: true);
                    enumCount++;
                }
            }

            Assert.True(enumCount >= 10,
                $"Expected >= 10 enum files, got {enumCount}");

            // Write a minimal .csproj
            var csproj = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>Library</OutputType>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(projDir, "CompileTest.csproj"), csproj);

            // Build the project
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build --nologo --verbosity quiet",
                WorkingDirectory = projDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(TimeSpan.FromMinutes(3));

            Assert.True(process.ExitCode == 0,
                $"Emitted enum files failed to compile ({enumCount} files).\n" +
                $"Stdout:\n{stdout}\nStderr:\n{stderr}");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch { /* best effort cleanup */ }
        }
    }

    /// <summary>
    /// Verifies the model.json from the extractor is valid and contains expected structures.
    /// </summary>
    [Fact]
    public void ExtractorOutput_ModelIsValid()
    {
        var modelPath = EmitterTestHelper.GetFullModelPath();
        var json = File.ReadAllText(modelPath);
        var model = JsonSerializer.Deserialize<MonacoModel>(json);

        Assert.NotNull(model);
        Assert.Equal(1, model.SchemaVersion);
        Assert.NotEmpty(model.Namespaces);

        // Must have the core namespaces
        var nsNames = model.Namespaces.Select(n => n.Name).ToHashSet();
        Assert.Contains("monaco", nsNames);
        Assert.Contains("monaco.editor", nsNames);
        Assert.Contains("monaco.languages", nsNames);

        // Must have key types
        var allEnums = model.Namespaces.SelectMany(n => n.Enums).Select(e => e.Name).ToHashSet();
        var allInterfaces = model.Namespaces.SelectMany(n => n.Interfaces).Select(i => i.Name).ToHashSet();

        Assert.Contains("MarkerSeverity", allEnums);
        Assert.Contains("CompletionItemKind", allEnums);
        Assert.Contains("IPosition", allInterfaces);
        Assert.Contains("IRange", allInterfaces);
    }

    /// <summary>
    /// Verifies that the full pipeline produces deterministic output:
    /// running the emitter twice on the same model produces identical files.
    /// </summary>
    [Fact]
    public void DeterministicOrdering_IdenticalOutput()
    {
        var modelPath = EmitterTestHelper.GetFullModelPath();
        var model = EmitterTestHelper.LoadModel(modelPath);

        var files1 = EmitterTestHelper.EmitToMemory(model);
        var files2 = EmitterTestHelper.EmitToMemory(model);

        // Same number of files
        Assert.Equal(files1.Count, files2.Count);

        // Same file names
        var keys1 = files1.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        var keys2 = files2.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(keys1, keys2);

        // Same content for each file
        foreach (var key in keys1)
        {
            Assert.True(files1[key] == files2[key],
                $"File '{key}' differs between runs. Emitter output is not deterministic.");
        }
    }

}
