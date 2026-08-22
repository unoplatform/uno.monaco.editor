using Microsoft.UI.Xaml;

using Monaco.Editor;

namespace Monaco
{
    partial class DiffCodeEditor
    {
        /// <summary>
        /// Gets or sets the original (left-hand) document -- the "before" side of the comparison.
        /// </summary>
        /// <remarks>
        /// Read-only by default. Set <see cref="DiffOptions"/>.<see cref="DiffEditorOptions.OriginalEditable"/>
        /// to make it editable; note that edits to it are not pushed back to this property.
        /// </remarks>
        public string OriginalText
        {
            get => (string)GetValue(OriginalTextProperty);
            set => SetValue(OriginalTextProperty, value);
        }

        /// <summary>Identifies the <see cref="OriginalText"/> dependency property.</summary>
        public static DependencyProperty OriginalTextProperty { get; } = DependencyProperty.Register(nameof(OriginalText), typeof(string), typeof(DiffCodeEditor), new PropertyMetadata(string.Empty, async (d, e) =>
        {
            if (d is DiffCodeEditor editor)
            {
                if (editor.IsEditorLoaded && !editor.IsSettingValue)
                {
                    // link:otherScriptsToBeOrganized.ts:updateOriginalContent
                    await editor.InvokeScriptAsync("updateOriginalContent", e.NewValue != null ? e.NewValue.ToString() : string.Empty);
                }

                editor.NotifyPropertyChanged(nameof(OriginalText));
            }
        }));

        /// <summary>
        /// Gets or sets the modified (right-hand) document -- the "after" side of the comparison,
        /// and the editable one.
        /// </summary>
        /// <remarks>
        /// This is the diff editor's counterpart to <see cref="CodeEditor.Text"/>, and the
        /// property the bridge writes back to as the user types. Every other inherited member
        /// that acts on "the" document -- selection, decorations, markers, cursor position --
        /// acts on this side.
        /// </remarks>
        public string ModifiedText
        {
            get => (string)GetValue(ModifiedTextProperty);
            set => SetValue(ModifiedTextProperty, value);
        }

        /// <summary>Identifies the <see cref="ModifiedText"/> dependency property.</summary>
        public static DependencyProperty ModifiedTextProperty { get; } = DependencyProperty.Register(nameof(ModifiedText), typeof(string), typeof(DiffCodeEditor), new PropertyMetadata(string.Empty, async (d, e) =>
        {
            if (d is DiffCodeEditor editor)
            {
                if (editor.IsEditorLoaded && !editor.IsSettingValue)
                {
                    // link:otherScriptsToBeOrganized.ts:updateContent
                    await editor.InvokeScriptAsync("updateContent", e.NewValue != null ? e.NewValue.ToString() : string.Empty);
                }

                editor.NotifyPropertyChanged(nameof(ModifiedText));
            }
        }));

        /// <summary>
        /// Gets or sets the syntax language of the original document. When unset, the original
        /// side follows <see cref="CodeEditorBase.CodeLanguage"/>.
        /// </summary>
        /// <remarks>
        /// Only worth setting when the two sides are genuinely different languages -- comparing
        /// a config file against its rendered output, for instance.
        /// </remarks>
        public string OriginalLanguage
        {
            get => (string)GetValue(OriginalLanguageProperty);
            set => SetValue(OriginalLanguageProperty, value);
        }

        /// <summary>Identifies the <see cref="OriginalLanguage"/> dependency property.</summary>
        public static DependencyProperty OriginalLanguageProperty { get; } = DependencyProperty.Register(nameof(OriginalLanguage), typeof(string), typeof(DiffCodeEditor), new PropertyMetadata(null, async (d, e) =>
        {
            if (d is DiffCodeEditor editor && editor.IsEditorLoaded)
            {
                // link:otherScriptsToBeOrganized.ts:updateOriginalLanguage
                await editor.InvokeScriptAsync("updateOriginalLanguage", editor.EffectiveOriginalLanguage);
            }
        }));

        /// <summary>
        /// Gets or sets the diff-specific options: side-by-side versus inline rendering,
        /// whitespace handling, the diff algorithm, collapsing of unchanged regions, and so on.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="CodeEditorBase.Options"/>, which configures the underlying
        /// editor and is applied to the modified side. Monaco keeps the two in separate sinks
        /// and each ignores the other's keys, so they are not interchangeable.
        /// <para>
        /// Changes to individual properties on the assigned instance are forwarded to Monaco
        /// automatically. The nested <see cref="DiffEditorOptions.Experimental"/> and
        /// <see cref="DiffEditorOptions.HideUnchangedRegions"/> objects are plain values and do
        /// not raise change notifications -- assign a new instance to push an update.
        /// </para>
        /// </remarks>
        public DiffEditorOptions DiffOptions
        {
            get => (DiffEditorOptions)GetValue(DiffOptionsProperty);
            set => SetValue(DiffOptionsProperty, value);
        }

        /// <summary>Identifies the <see cref="DiffOptions"/> dependency property.</summary>
        public static DependencyProperty DiffOptionsProperty { get; } = DependencyProperty.Register(
            nameof(DiffOptions),
            typeof(DiffEditorOptions),
            typeof(DiffCodeEditor),
            new PropertyMetadata(
                null,
                async (d, e) =>
                {
                    if (d is not DiffCodeEditor editor) return;

                    // Subscribing here (rather than on load, as the inherited Options property
                    // does) is enough: the options object lives as long as the control, and
                    // pre-initialization pushes are no-ops that ApplyInitialPropertyValues
                    // covers when the editor comes up.
                    if (e.OldValue is DiffEditorOptions oldValue)
                    {
                        oldValue.PropertyChanged -= editor.DiffOptions_PropertyChanged;
                    }

                    if (e.NewValue is DiffEditorOptions value)
                    {
                        value.PropertyChanged -= editor.DiffOptions_PropertyChanged;
                        value.PropertyChanged += editor.DiffOptions_PropertyChanged;

                        if (editor.IsEditorLoaded)
                        {
                            await editor.InvokeScriptAsync("updateDiffOptions", value);
                        }
                    }
                }));
    }
}
