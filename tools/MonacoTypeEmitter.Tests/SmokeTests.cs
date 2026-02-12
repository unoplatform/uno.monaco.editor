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
    /// emit C# files, then compile the enum subset in an isolated temp project.
    /// This validates the full parse -> emit -> compile pipeline.
    ///
    /// Compilation scope: Enums are compiled because they are self-contained (no cross-type
    /// dependencies). Full model compilation is not feasible because Monaco's TypeScript API
    /// includes exotic identifiers ($ prefixes, dot-separated property names, 'is' as method
    /// names) that produce invalid C# -- these are known emitter limitations documented in the
    /// model. The serialization attribute chain (InterfaceToClassConverter, JsonStringEnumConverter,
    /// JsonPropertyName) is validated separately by FullPipeline_SerializationAttributes_Present
    /// and the WireFormatCompatibility_FullAttributeChain round-trip test.
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

            // Copy only enum files (files that contain "public enum"),
            // preserving directory structure to avoid filename collisions
            int enumCount = 0;
            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.EnumerateFiles(outputDir, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                if (content.Contains("public enum "))
                {
                    var relativePath = Path.GetRelativePath(outputDir, file);
                    var destPath = Path.Combine(projDir, relativePath);
                    var destDir = Path.GetDirectoryName(destPath)!;
                    Directory.CreateDirectory(destDir);

                    Assert.True(destinations.Add(destPath),
                        $"Duplicate destination path: {destPath}");
                    File.Copy(file, destPath, overwrite: true);
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
            var exited = process.WaitForExit(TimeSpan.FromMinutes(3));

            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch { /* best effort */ }
                Assert.Fail(
                    $"Build process timed out after 3 minutes ({enumCount} enum files).\n" +
                    $"Partial stdout:\n{stdout}\nPartial stderr:\n{stderr}");
            }

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
    /// Validates that emitted types include the correct serialization infrastructure
    /// required for JSON round-trip fidelity with MonacoJsonContext:
    /// - InterfaceToClassConverter on I-prefix interfaces (enables deserialization)
    /// - JsonStringEnumConverter + EnumMember on string enum type aliases
    /// - Concrete sealed classes implementing their interface counterparts
    /// - Correct absence of JsonStringEnumConverter on numeric enums
    ///
    /// This validates the converter/context serialization contract that
    /// SerializationContractTests exercises at runtime. The emitter's responsibility
    /// is producing code with the correct attributes; the runtime behavior is
    /// verified by the existing SerializationContractTests suite (~40 tests).
    /// </summary>
    [Fact]
    public void FullPipeline_SerializationAttributes_Present()
    {
        var model = EmitterTestHelper.LoadModel(EmitterTestHelper.GetFullModelPath());
        var files = EmitterTestHelper.EmitToMemory(model);

        // Verify concrete classes implement the corresponding interface
        // (MarkerData : IMarkerData pattern for InterfaceToClassConverter round-trip)
        var markerDataContent = files.First(f =>
            f.Key.EndsWith("/MarkerData.cs", StringComparison.OrdinalIgnoreCase)
            || f.Key == "MarkerData.cs").Value;
        Assert.Contains(": IMarkerData", markerDataContent);
        Assert.Contains("get; set;", markerDataContent);
        // MarkerData property names map naturally via camelCase naming policy,
        // so [JsonPropertyName] is correctly omitted when the PascalCase C# name
        // maps to the exact camelCase TS name (e.g., Severity -> severity)

        // Verify InterfaceToClassConverter on interface-typed properties
        // IMarkerData -> MarkerData should have the converter attribute
        var iMarkerDataContent = files.First(f =>
            f.Key.EndsWith("IMarkerData.cs", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.Contains("InterfaceToClassConverter", iMarkerDataContent);

        // Verify string enums have JsonStringEnumConverter
        var builtinThemeContent = files.First(f =>
            f.Key.EndsWith("BuiltinTheme.cs", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.Contains("JsonStringEnumConverter<BuiltinTheme>", builtinThemeContent);
        Assert.Contains("EnumMember", builtinThemeContent);

        // Verify numeric enums do NOT have JsonStringEnumConverter
        var markerSeverityContent = files.First(f =>
            f.Key.EndsWith("MarkerSeverity.cs", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.DoesNotContain("JsonStringEnumConverter", markerSeverityContent);

        // Verify IRange interface has InterfaceToClassConverter for round-trip deserialization
        var iRangeContent = files.First(f =>
            f.Key.EndsWith("IRange.cs", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.Contains("InterfaceToClassConverter", iRangeContent);
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
