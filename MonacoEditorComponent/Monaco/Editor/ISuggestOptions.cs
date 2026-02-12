using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// Configuration options for editor suggest widget
    /// </summary>
    public sealed class ISuggestOptions
    {
        /// <summary>
        /// Enable graceful matching. Defaults to true.
        /// </summary>
        public bool? FilterGraceful { get; set; }

        /// <summary>
        /// Controls the visibility of the status bar at the bottom of the suggest widget.
        /// </summary>
        public bool? HideStatusBar { get; set; }

        /// <summary>
        /// Show a highlight when suggestion replaces or keep text after the cursor. Defaults to false.
        /// </summary>
        public bool? InsertHighlight { get; set; }

        /// <summary>
        /// Overwrite word ends on accept. Default to false.
        /// </summary>
        public InsertMode? InsertMode { get; set; }

        /// <summary>
        /// Favours words that appear close to the cursor.
        /// </summary>
        public bool? LocalityBonus { get; set; }

        /// <summary>
        /// Max suggestions to show in suggestions. Defaults to 12.
        /// </summary>
        public int? MaxVisibleSuggestions { get; set; }

        /// <summary>
        /// Enable using global storage for remembering suggestions.
        /// </summary>
        public bool? ShareSuggestSelections { get; set; }

        /// <summary>
        /// Show class-suggestions.
        /// </summary>
        public bool? ShowClasses { get; set; }

        /// <summary>
        /// Show color-suggestions.
        /// </summary>
        public bool? ShowColors { get; set; }

        /// <summary>
        /// Show constant-suggestions.
        /// </summary>
        public bool? ShowConstants { get; set; }

        /// <summary>
        /// Show constructor-suggestions.
        /// </summary>
        public bool? ShowConstructors { get; set; }

        /// <summary>
        /// Show enumMember-suggestions.
        /// </summary>
        public bool? ShowEnumMembers { get; set; }

        /// <summary>
        /// Show enum-suggestions.
        /// </summary>
        public bool? ShowEnums { get; set; }

        /// <summary>
        /// Show event-suggestions.
        /// </summary>
        public bool? ShowEvents { get; set; }

        /// <summary>
        /// Show field-suggestions.
        /// </summary>
        public bool? ShowFields { get; set; }

        /// <summary>
        /// Show file-suggestions.
        /// </summary>
        public bool? ShowFiles { get; set; }

        /// <summary>
        /// Show folder-suggestions.
        /// </summary>
        public bool? ShowFolders { get; set; }

        /// <summary>
        /// Show function-suggestions.
        /// </summary>
        public bool? ShowFunctions { get; set; }

        /// <summary>
        /// Enable or disable icons in suggestions. Defaults to true.
        /// </summary>
        public bool? ShowIcons { get; set; }

        /// <summary>
        /// Show interface-suggestions.
        /// </summary>
        public bool? ShowInterfaces { get; set; }

        /// <summary>
        /// Show keyword-suggestions.
        /// </summary>
        public bool? ShowKeywords { get; set; }

        /// <summary>
        /// Show method-suggestions.
        /// </summary>
        public bool? ShowMethods { get; set; }

        /// <summary>
        /// Show module-suggestions.
        /// </summary>
        public bool? ShowModules { get; set; }

        /// <summary>
        /// Show operator-suggestions.
        /// </summary>
        public bool? ShowOperators { get; set; }

        /// <summary>
        /// Show property-suggestions.
        /// </summary>
        public bool? ShowProperties { get; set; }

        /// <summary>
        /// Show reference-suggestions.
        /// </summary>
        public bool? ShowReferences { get; set; }

        /// <summary>
        /// Show snippet-suggestions.
        /// </summary>
        public bool? ShowSnippets { get; set; }

        /// <summary>
        /// Show struct-suggestions.
        /// </summary>
        public bool? ShowStructs { get; set; }

        /// <summary>
        /// Show typeParameter-suggestions.
        /// </summary>
        public bool? ShowTypeParameters { get; set; }

        /// <summary>
        /// Show unit-suggestions.
        /// </summary>
        public bool? ShowUnits { get; set; }

        /// <summary>
        /// Show value-suggestions.
        /// </summary>
        public bool? ShowValues { get; set; }

        /// <summary>
        /// Show variable-suggestions.
        /// </summary>
        public bool? ShowVariables { get; set; }

        /// <summary>
        /// Show text-suggestions.
        /// </summary>
        public bool? ShowWords { get; set; }

        /// <summary>
        /// Prevent quick suggestions when a snippet is active. Defaults to true.
        /// </summary>
        public bool? SnippetsPreventQuickSuggestions { get; set; }
    }
}
