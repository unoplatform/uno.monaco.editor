#nullable enable

namespace MonacoTypeEmitter.Emitter;

/// <summary>
/// Manages the list of files that should not be overwritten by the generator.
/// Uses repo-relative path matching for unambiguous resolution.
/// </summary>
public sealed class IgnoreList
{
    private readonly HashSet<string> _ignoredPaths;

    private IgnoreList(HashSet<string> ignoredPaths)
    {
        _ignoredPaths = ignoredPaths;
    }

    /// <summary>
    /// Loads the ignore list from a file. Each line is a repo-relative path
    /// (e.g., "MonacoEditorComponent/Monaco/Editor/CompletionItem.cs").
    /// Lines starting with # are comments. Empty lines are skipped.
    /// </summary>
    public static IgnoreList Load(string filePath)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(filePath))
            return new IgnoreList(paths);

        foreach (var line in File.ReadAllLines(filePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            // Normalize path separators
            paths.Add(trimmed.Replace('\\', '/'));
        }

        return new IgnoreList(paths);
    }

    /// <summary>
    /// Checks whether the given repo-relative path is in the ignore list.
    /// </summary>
    public bool IsIgnored(string repoRelativePath)
    {
        var normalized = repoRelativePath.Replace('\\', '/');
        return _ignoredPaths.Contains(normalized);
    }

    /// <summary>
    /// Validates that all entries in the ignore list resolve to actual files
    /// within the given repository root. Returns a list of entries that do not
    /// resolve to a unique file.
    /// </summary>
    public List<string> Validate(string repoRoot)
    {
        var errors = new List<string>();

        foreach (var path in _ignoredPaths)
        {
            var fullPath = Path.Combine(repoRoot, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                errors.Add($"Ignore list entry does not resolve to an existing file: {path}");
            }
        }

        return errors;
    }

    public int Count => _ignoredPaths.Count;
}
