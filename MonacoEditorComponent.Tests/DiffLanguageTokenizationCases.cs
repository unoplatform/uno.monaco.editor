namespace MonacoEditorComponent.Tests;

/// <summary>
/// Shared sample and expectations for the bundled <c>diff</c> language, consumed by both
/// the WASM and desktop integration tests so the two cannot drift apart.
/// <para>Monaco ships no diff grammar, so the component registers a Monarch one at bundle
/// load (<c>ts-helpermethods/languages/diff.ts</c>). What these cases actually guard is
/// the grammar's rule <i>order</i>: several diff dialects open lines with the same marker,
/// so whichever rule comes first wins. The two that bite are a context-diff range
/// (<c>*** 1,4 ****</c>, <c>--- 1,4 ----</c>) versus a unified from-file header
/// (<c>--- a/file</c>), and a normal diff's bare <c>---</c> separator versus a deleted
/// line, which starts with the same character.</para>
/// </summary>
internal static class DiffLanguageTokenizationCases
{
    /// <summary>
    /// One line per feature across the four diff dialects (git/unified, context, normal,
    /// combined), paired with the token type Monaco should report for it. The empty
    /// expectation is Monarch's default token, used for unchanged context lines.
    /// </summary>
    public static readonly (string Line, string ExpectedToken)[] Cases =
    [
        ("diff --git a/a.txt b/a.txt", "type.header.diff"),
        ("index 0000000..1111111 100644", "type.header.diff"),
        ("--- a/a.txt", "type.header.diff"),
        ("+++ b/a.txt", "type.header.diff"),
        ("@@ -1,3 +1,3 @@ section", "keyword.flow.range.diff"),
        (" context", ""),
        ("-removed", "string.delete.diff"),
        ("+added", "comment.insert.diff"),
        ("\\ No newline at end of file", "type.meta.diff"),
        ("***************", "type.meta.diff"),
        ("*** 1,4 ****", "keyword.flow.range.diff"),
        ("--- 1,4 ----", "keyword.flow.range.diff"),
        ("! changed", "keyword.change.diff"),
        ("1,2c3,4", "keyword.flow.range.diff"),
        ("< left", "string.delete.diff"),
        ("> right", "comment.insert.diff"),
        ("---", "type.header.diff"),
        ("@@@ -1,2 -1,2 +1,3 @@@", "keyword.flow.range.diff"),
    ];

    /// <summary>The sample document, passed to the page as an argument so no escaping is needed.</summary>
    public static string Sample => string.Join("\n", Cases.Select(c => c.Line));

    /// <summary>Expected result of <see cref="TokenizeExpression"/> over <see cref="Sample"/>.</summary>
    public static string ExpectedTokens => string.Join(",", Cases.Select(c => c.ExpectedToken));

    /// <summary>
    /// Reports the first token type on each line. Only the first is compared: a hunk header
    /// is tokenized through Monarch's groups form, whose trailing group is empty when the
    /// header carries no section text, and that emits a zero-width token the assertion has
    /// no reason to care about.
    /// </summary>
    public const string TokenizeExpression =
        "(sample) => monaco.editor.tokenize(sample, 'diff').map(line => line.length ? line[0].type : '').join(',')";

    /// <summary>Whether Monaco reports <c>diff</c> in its language registry.</summary>
    public const string IsRegisteredExpression =
        "() => monaco.languages.getLanguages().some(language => language.id === 'diff')";
}
