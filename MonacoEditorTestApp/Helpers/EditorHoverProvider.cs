using Monaco;
using Monaco.Editor;
using Monaco.Languages;
using System;
using System.Threading.Tasks;
using Windows.Foundation;

namespace MonacoEditorTestApp.Helpers
{
    class EditorHoverProvider(Func<string> textProvider) : HoverProvider
    {
        public Task<Hover?> ProvideHover(IModel model, Position position)
        {
            var word = TryGetWordAt(textProvider(), position);
            if (word is null || !string.Equals(word.Value.Word, "Hit", StringComparison.Ordinal))
            {
                return Task.FromResult<Hover?>(null);
            }

            return Task.FromResult<Hover?>(new Hover(
            [
                    "*Hit* - press the keys following together.",
                    "Some **more** text is here.",
                    "And a [link](https://www.github.com/)."
            ],
            new Monaco.Range(position.LineNumber, word.Value.StartColumn, position.LineNumber, word.Value.EndColumn)));
        }

        private static (string Word, uint StartColumn, uint EndColumn)? TryGetWordAt(string text, Position position)
        {
            if (string.IsNullOrEmpty(text) || position.LineNumber == 0 || position.Column == 0)
            {
                return null;
            }

            var lines = text.Split('\n');
            var lineIndex = (int)position.LineNumber - 1;
            if (lineIndex < 0 || lineIndex >= lines.Length)
            {
                return null;
            }

            var line = lines[lineIndex].TrimEnd('\r');
            if (line.Length == 0)
            {
                return null;
            }

            var columnIndex = Math.Min(Math.Max((int)position.Column - 1, 0), line.Length - 1);
            if (!IsWordChar(line[columnIndex]))
            {
                var leftIndex = columnIndex - 1;
                if (leftIndex < 0 || !IsWordChar(line[leftIndex]))
                {
                    return null;
                }

                columnIndex = leftIndex;
            }

            var start = columnIndex;
            while (start > 0 && IsWordChar(line[start - 1]))
            {
                start--;
            }

            var end = columnIndex + 1;
            while (end < line.Length && IsWordChar(line[end]))
            {
                end++;
            }

            var word = line.Substring(start, end - start);
            return (word, (uint)start + 1, (uint)end + 1);
        }

        private static bool IsWordChar(char value)
            => char.IsLetterOrDigit(value) || value == '_';
    }
}
