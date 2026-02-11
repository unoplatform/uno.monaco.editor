using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    public sealed class EditorFindOptions
    {
        public bool? AddExtraSpaceOnTop { get; set; }

        /// <summary>
        /// Controls if Find in Selection flag is turned on in the editor.
        /// </summary>
        public AutoFindInSelection? AutoFindInSelection { get; set; }

        /// <summary>
        /// Controls if we seed search string in the Find Widget with editor selection.
        /// </summary>
        public bool? SeedSearchStringFromSelection { get; set; }
    }

}
