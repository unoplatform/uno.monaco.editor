using Monaco.Editor;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Monaco;
using Windows.UI.Popups;

namespace MonacoEditorTestApp.Actions
{
    class TestAction : IActionDescriptor
    {
        public string? ContextMenuGroupId => "navigation";
        public float ContextMenuOrder => 1.5f;
        public string Id => "meta-test-action";
        public string? KeybindingContext => "editorHasSelection";
        public int[] Keybindings => [Monaco.KeyMod.Chord(Monaco.KeyMod.CtrlCmd | Monaco.KeyCode.KEY_K, Monaco.KeyMod.CtrlCmd | Monaco.KeyCode.KEY_M)];
        public string? Label => "Test Action";
        public string? Precondition => "editorHasSelection";

        public async void Run(EditorHostBase editor, object[]? args)
        {
            var selectedText = editor.SelectedText ?? string.Empty;
            if (args is { Length: > 0 })
            {
                selectedText = args[0] switch
                {
                    string text => text,
                    JsonElement { ValueKind: JsonValueKind.String } json => json.GetString() ?? string.Empty,
                    _ => selectedText
                };
            }

            var md = new MessageDialog("You have selected text:\n\n" + selectedText);
            if (App.MainWindow is not null)
            {
                WinRT.Interop.InitializeWithWindow.Initialize(md, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
            }
            await md.ShowAsync();

            editor.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
    }
}
