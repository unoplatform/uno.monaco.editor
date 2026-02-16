using Monaco;
using Monaco.Editor;
using Monaco.Languages;
using System;
using System.Threading.Tasks;
using Windows.Foundation;

namespace MonacoEditorTestApp.Helpers
{
    class EditorHoverProvider : HoverProvider
    {
        public async Task<Hover?> ProvideHover(IModel model, Position position)
        {
            var wordTask = model.GetWordAtPositionAsync(position);
            if (await Task.WhenAny(wordTask, Task.Delay(300)) != wordTask)
            {
                return null;
            }

            var word = await wordTask;
            if (word is null || !string.Equals(word.Word, "Hit", StringComparison.Ordinal))
            {
                return null;
            }

            return new Hover(
            [
                    "*Hit* - press the keys following together.",
                    "Some **more** text is here.",
                    "And a [link](https://www.github.com/)."
            ],
            new Monaco.Range(position.LineNumber, word.StartColumn, position.LineNumber, word.EndColumn));
        }
    }
}
