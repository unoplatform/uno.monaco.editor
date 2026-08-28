using Microsoft.UI.Xaml;

namespace Monaco
{
    partial class CodeEditor
    {
        /// <summary>
        /// Gets or sets the text content of the editor.
        /// </summary>
        /// <remarks>
        /// Setting this property after the editor is loaded invokes the Monaco
        /// <c>editor.setValue</c> API via <c>updateContent</c>. Changes originating
        /// from JavaScript are pushed back through the bridge and suppress re-entrant
        /// notifications via <see cref="EditorHostBase.IsSettingValue"/>.
        /// </remarks>
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>Identifies the <see cref="Text"/> dependency property.</summary>
        public static DependencyProperty TextProperty { get; } = DependencyProperty.Register(nameof(Text), typeof(string), typeof(CodeEditor), new PropertyMetadata(string.Empty, async (d, e) =>
        {
            if (d is CodeEditor codeEditor)
            {
                if (codeEditor.IsEditorLoaded && !codeEditor.IsSettingValue)
                {
                    // link:otherScriptsToBeOrganized.ts:updateContent
                    await codeEditor.InvokeScriptAsync("updateContent", e.NewValue != null ? e.NewValue.ToString() : string.Empty);
                }

                codeEditor.NotifyPropertyChanged(nameof(Text));
            }
        }));
    }
}
