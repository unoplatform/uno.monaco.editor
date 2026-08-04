import * as monaco from 'monaco-editor';

/**
 * Built-in `diff` (patch) language.
 *
 * Monaco ships no `diff` grammar of its own: VS Code's diff highlighting comes from a
 * built-in extension carrying a TextMate grammar, and Monaco only supports Monarch, so
 * that grammar was never ported. Without this module `CodeLanguage = "diff"` renders
 * unhighlighted -- Monaco resolves unregistered language ids to `plaintext` instead of
 * failing -- which is easy to mistake for a broken editor.
 *
 * Registered once at bundle load (see index.ts) so `diff` behaves like any language
 * Monaco does ship: it appears in `monaco.languages.getLanguages()` (and therefore in
 * `LanguagesHelper.GetLanguagesAsync()`), and `.diff` / `.patch` resolve through
 * `languageIdFromExtension`. No other bundled language claims those two extensions.
 */

export const diffLanguageId = 'diff';

export const diffLanguageExtensionPoint: monaco.languages.ILanguageExtensionPoint = {
    id: diffLanguageId,
    extensions: ['.diff', '.patch'],
    aliases: ['Diff', 'diff', 'patch'],
    mimetypes: ['text/x-diff', 'text/x-patch'],
};

/**
 * Monarch grammar covering the four diff dialects that tools in the wild emit:
 * git/unified (`diff -u`), context (`diff -c`), normal (`diff` with no flags), and
 * combined (`diff --cc`, merge diffs, which use doubled `@@@` and prefix columns).
 *
 * Token names are deliberately built on Monaco's *themed* token vocabulary rather than
 * VS Code's TextMate scope names (`markup.inserted` and friends). Monaco's built-in
 * themes -- `vs`, `vs-dark`, `hc-black`, `hc-light` -- only assign colors to their own
 * vocabulary, and theme lookup matches on a dot-segment *prefix*, so `comment.insert`
 * inherits whatever the active theme gives `comment`. That is what makes highlighting
 * work in all four themes without redefining them: a library that repainted the host's
 * themes at import time would silently lose to any consumer that later called
 * `defineTheme`, and would leak diff styling into every other editor on the page.
 *
 * The resulting palette (insert green, delete red/salmon, changed blue, headers teal,
 * ranges purple) is close to conventional diff coloring but not identical to VS Code's.
 * A consumer who wants exact parity can override the emitted token types -- which carry
 * the `.diff` postfix Monarch appends, e.g. `comment.insert.diff` -- in their own theme.
 *
 * Renaming these tokens is not purely cosmetic: Monaco derives a token's *standard* type
 * by matching `comment|string|regex|regexp` against any segment, so inserted lines are
 * additionally classified as comments and deleted lines as strings. That is accepted
 * here because the classification only drives bracket-pair colorization, auto-closing
 * pairs, and comment toggling -- all of which need a language configuration that `diff`
 * deliberately does not register, and none of which are wanted inside patch text anyway.
 */
export const diffMonarchLanguage: monaco.languages.IMonarchLanguage = {
    defaultToken: '',
    tokenPostfix: '.diff',

    // Diff is line-oriented, so every rule matches a whole line and is anchored with
    // `^`. Monarch reads a leading `^` as "column 0 only" and strips it before
    // compiling, so no rule here can match mid-line -- which is what makes the ordering
    // below meaningful rather than incidental.
    tokenizer: {
        root: [
            // -- Driver and git extended headers ------------------------------------
            [/^diff\b.*$/, 'type.header'],
            [/^(?:index|old mode|new mode|new file mode|deleted file mode|similarity index|dissimilarity index|copy from|copy to|rename from|rename to)\b.*$/, 'type.header'],
            [/^(?:Binary files|Files)\b.*differ$/, 'type.header'],
            [/^GIT binary patch$/, 'type.header'],
            [/^Only in\b.*$/, 'type.header'],

            // -- Ranges -------------------------------------------------------------
            // These must precede the file-header rules below: a context-diff range
            // (`*** 1,4 ****`, `--- 1,4 ----`) opens with the same `***`/`---` marker as
            // a from-file header, so ordering alone decides which wins.
            [/^\*{3} \d+(?:,\d+)? \*{4}$/, 'keyword.flow.range'],
            [/^-{3} \d+(?:,\d+)? -{4}$/, 'keyword.flow.range'],
            // Unified `@@ -1,4 +1,6 @@`, combined `@@@ -1,2 -1,2 +1,3 @@@`. The trailing
            // section heading git appends is left to the default token, matching how
            // VS Code renders it as ordinary code rather than part of the range.
            [/^(@@+)([^@]*)(@@+)(.*)$/, ['keyword.flow.range', 'keyword.flow.range', 'keyword.flow.range', '']],
            // Normal diff: `1,2c3,4`, `5d4`, `0a1`.
            [/^\d+(?:,\d+)?[acd]\d+(?:,\d+)?$/, 'keyword.flow.range'],

            // -- File headers -------------------------------------------------------
            // `--- a/f`, `+++ b/f`, `*** f`, and the bare `---` that separates the two
            // halves of a normal diff. The bare form has to be caught here, otherwise it
            // falls through to the deleted-line rule and reads as removed content.
            [/^(?:-{3}|\+{3}|\*{3})(?:[ \t].*)?$/, 'type.header'],
            // Context diff's `***************` block separator.
            [/^\*{4,}$/, 'type.meta'],

            // -- Content ------------------------------------------------------------
            // `+`/`-` are unified and combined diffs (combined doubles the prefix, e.g.
            // `++added`); `>`/`<` are normal diffs. A combined diff's " +" form -- one
            // parent unchanged, space in the first column -- stays a context line, as it
            // is in the merge result either way.
            [/^[+>].*$/, 'comment.insert'],
            [/^[-<].*$/, 'string.delete'],
            // Context diff's changed marker.
            [/^!.*$/, 'keyword.change'],

            // `\ No newline at end of file` -- metadata, not content.
            [/^\\.*$/, 'type.meta'],

            // Unchanged/context lines.
            [/^.*$/, ''],
        ],
    },
};

/**
 * Registers the `diff` language and its tokenizer.
 *
 * Idempotent: the bundle assigns its exports onto `globalThis` and can be evaluated more
 * than once in a page, and registering twice would stack a second tokenizer on the same
 * language id. Tokenization is wired up eagerly rather than through
 * `monaco.languages.onLanguage`, because compiling a grammar this small costs less than
 * the lazy path's failure mode of silently never highlighting.
 */
export function registerDiffLanguage(): void {
    if (monaco.languages.getLanguages().some(language => language.id === diffLanguageId)) {
        return;
    }

    monaco.languages.register(diffLanguageExtensionPoint);
    monaco.languages.setMonarchTokensProvider(diffLanguageId, diffMonarchLanguage);
}
