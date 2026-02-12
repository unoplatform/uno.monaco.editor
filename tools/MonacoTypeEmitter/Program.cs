#nullable enable

using System.Text.Json;
using MonacoTypeEmitter.Emitter;
using MonacoTypeEmitter.Model;

// Parse command line arguments
string? inputPath = null;
string? outputPath = null;
string? ignoreFile = null;
string? repoRoot = null;
bool validate = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--input" or "-i":
            inputPath = args[++i];
            break;
        case "--output" or "-o":
            outputPath = args[++i];
            break;
        case "--ignore-file":
            ignoreFile = args[++i];
            break;
        case "--repo-root":
            repoRoot = args[++i];
            break;
        case "--validate":
            validate = true;
            break;
        case "--help" or "-h":
            PrintUsage();
            return 0;
        default:
            Console.Error.WriteLine($"Error: Unknown option: {args[i]}");
            PrintUsage();
            return 1;
    }
}

if (inputPath is null)
{
    Console.Error.WriteLine("Error: --input is required");
    PrintUsage();
    return 1;
}

if (outputPath is null)
{
    Console.Error.WriteLine("Error: --output is required");
    PrintUsage();
    return 1;
}

// Resolve paths
inputPath = Path.GetFullPath(inputPath);
outputPath = Path.GetFullPath(outputPath);

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
    return 1;
}

// Determine repo root (default: walk up from output looking for .git)
repoRoot = repoRoot is not null
    ? Path.GetFullPath(repoRoot)
    : FindRepoRoot(outputPath);

if (repoRoot is null)
{
    Console.Error.WriteLine("Error: Could not determine repository root. Use --repo-root.");
    return 1;
}

// Load ignore file
ignoreFile ??= Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? ".",
    ".generator-ignore");

// Also check next to the tool's project directory
if (!File.Exists(ignoreFile))
{
    var toolDir = FindToolDirectory();
    if (toolDir is not null)
        ignoreFile = Path.Combine(toolDir, ".generator-ignore");
}

var ignoreList = IgnoreList.Load(ignoreFile);
Console.Error.WriteLine($"Loaded {ignoreList.Count} entries from .generator-ignore");

// Validate ignore list if requested
if (validate)
{
    var errors = ignoreList.Validate(repoRoot);
    if (errors.Count > 0)
    {
        Console.Error.WriteLine("Ignore list validation errors:");
        foreach (var error in errors)
            Console.Error.WriteLine($"  {error}");
        return 1;
    }
    Console.Error.WriteLine("Ignore list validation passed.");
}

// Load model
Console.Error.WriteLine($"Reading model from: {inputPath}");
var json = File.ReadAllText(inputPath);
var model = JsonSerializer.Deserialize<MonacoModel>(json);

if (model is null)
{
    Console.Error.WriteLine("Error: Failed to deserialize model.");
    return 1;
}

Console.Error.WriteLine($"Model schema version: {model.SchemaVersion}");
Console.Error.WriteLine($"Namespaces: {model.Namespaces.Count}");

// Emit
Console.Error.WriteLine($"Emitting C# files to: {outputPath}");
var emitter = new CSharpEmitter(model, ignoreList, outputPath, repoRoot);
var written = emitter.EmitAll();

Console.Error.WriteLine();
Console.Error.WriteLine($"Emission complete: {written.Count} files written");
return 0;

// --- Helper methods ---

static void PrintUsage()
{
    Console.Error.WriteLine("""

        Usage: MonacoTypeEmitter --input <model.json> --output <dir> [options]

        Options:
          -i, --input <file>       Path to intermediate JSON model (required)
          -o, --output <dir>       Output directory for C# files (required)
          --ignore-file <file>     Path to .generator-ignore file
          --repo-root <dir>        Repository root for path resolution
          --validate               Validate ignore list entries resolve to files
          -h, --help               Show this help message

        Examples:
          dotnet run --project tools/MonacoTypeEmitter -- \
            --input tools/monaco-type-extractor/output/model.json \
            --output MonacoEditorComponent/Monaco/
        """);
}

static string? FindRepoRoot(string startPath)
{
    var dir = Directory.Exists(startPath) ? startPath : Path.GetDirectoryName(startPath);
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir, ".git")))
            return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}

static string? FindToolDirectory()
{
    // Walk up from current directory looking for MonacoTypeEmitter.csproj
    var dir = Directory.GetCurrentDirectory();
    while (dir is not null)
    {
        var toolDir = Path.Combine(dir, "tools", "MonacoTypeEmitter");
        if (Directory.Exists(toolDir))
            return toolDir;
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}
