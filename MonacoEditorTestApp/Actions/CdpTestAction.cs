using Monaco;
using Monaco.Editor;

namespace MonacoEditorTestApp.Actions;

/// <summary>
/// Lightweight action descriptor used by the CDP test harness.
/// When the action runs, it invokes the provided callback (which writes
/// a <c>TEST_CALLBACK:Action{id}:invoked</c> line to stdout).
/// </summary>
internal sealed class CdpTestAction(string id, Action callback) : IActionDescriptor
{
    public string? ContextMenuGroupId => null;
    public float ContextMenuOrder => 0;
    public string Id => id;
    public string? KeybindingContext => null;
    public int[] Keybindings => [];
    public string? Label => "CDP Test Action";
    public string? Precondition => null;

    public void Run(EditorHostBase editor, object[]? args)
    {
        callback();
    }
}
