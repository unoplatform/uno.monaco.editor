using Monaco;
using Monaco.Editor;
using Monaco.Languages;
using System.Threading.Tasks;
using Windows.Foundation;

namespace MonacoEditorTestApp.Helpers
{
    class EditorHoverProvider : HoverProvider
    {
        public Task<Hover?> ProvideHover(IModel model, Position position)
        {
            // Avoid re-entrant model callbacks from hover provider execution path.
            // Returning a deterministic tooltip for the instructions area keeps hover responsive on desktop.
            if (position.LineNumber >= 7 && position.LineNumber <= 12)
            {
                return Task.FromResult<Hover?>(new Hover(
                [
                        "*Hit* - press the keys following together.",
                        "Some **more** text is here.",
                        "And a [link](https://www.github.com/)."
                ], new Monaco.Range(position.LineNumber, position.Column, position.LineNumber, position.Column + 3)));
            }

            return Task.FromResult<Hover?>(new Hover(
            [
                "Hover provider is active."
            ],
            new Monaco.Range(position.LineNumber, position.Column, position.LineNumber, position.Column + 1)));
        }
    }
}
