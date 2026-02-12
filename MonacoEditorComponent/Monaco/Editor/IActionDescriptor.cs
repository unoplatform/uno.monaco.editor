
#if !NETSTANDARD2_0
using System.Runtime.InteropServices.WindowsRuntime;
#else
using ReadOnlyArrayAttribute = Monaco.Helpers.Stubs.ReadOnlyArrayAttribute;
using System.Text.Json.Serialization;
#endif

namespace Monaco.Editor
{
    /// <summary>
    /// Description of an action contribution
    /// </summary>
    public interface IActionDescriptor
    {
        /**
         * https://github.com/Microsoft/vscode/blob/master/src/vs/monaco.d.ts#L1907
		 * Control if the action should show up in the context menu and where.
		 * The context menu of the editor has these default:
		 *   navigation - The navigation group comes first in all cases.
		 *   1_modification - This group comes next and contains commands that modify your code.
		 *   9_cutcopypaste - The last default group with the basic editing commands.
		 * You can also create your own group.
		 * Defaults to null (don't show in context menu).
		 */
        string? ContextMenuGroupId { get; }

        float ContextMenuOrder { get; }

        string? Id { get; }

        /// <summary>
        /// <see cref="IContextKey"/>
        /// </summary>
        string? KeybindingContext { get; }

        /// <summary>
        /// <see cref="KeyMod"/>, <see cref="KeyCode"/>, and <see cref="KeyMod.Chord(int, int)"/>
        /// </summary>
        int[] Keybindings { get; }

        string? Label { get; }

        /// <summary>
        /// <see cref="IContextKey"/>
        /// </summary>
        string? Precondition { get; }

        void Run(CodeEditor editor, object[]? args);
    }
}
