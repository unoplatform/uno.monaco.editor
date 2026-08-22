using Microsoft.UI.Xaml;

using Monaco.Editor;
using Monaco.Helpers;

using Nito.AsyncEx;

using Windows.Foundation.Collections;

namespace Monaco
{
    abstract partial class CodeEditorBase : IParentAccessorAcceptor
    {
        /// <summary>
        /// Gets the helper for accessing <c>monaco.languages.*</c> registration APIs such as
        /// completion, hover, code-action, and code-lens providers.
        /// </summary>
        /// <remarks>
        /// Wraps <see href="https://microsoft.github.io/monaco-editor/typedoc/modules/editor_editor_api.languages.html">monaco.languages</see>.
        /// </remarks>
        public LanguagesHelper Languages { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the editor text is currently being set
        /// by the bridge layer, suppressing re-entrant change notifications.
        /// </summary>
        public bool IsSettingValue { get; set; }

        /// <summary>
        /// Gets or sets the currently selected text in the primary selection of the editor.
        /// </summary>
        public string SelectedText
        {
            get => (string)GetValue(SelectedTextProperty);
            set => SetValue(SelectedTextProperty, value);
        }

        /// <summary>Identifies the <see cref="SelectedText"/> dependency property.</summary>
        public static DependencyProperty SelectedTextProperty { get; } = DependencyProperty.Register(nameof(SelectedText), typeof(string), typeof(CodeEditorBase), new PropertyMetadata(string.Empty, (d, e) =>
        {
            if (d is CodeEditorBase codeEditor)
            {
                if (codeEditor.IsEditorLoaded && !codeEditor.IsSettingValue)
                {
                    // link:updateSelectedContent.ts:updateSelectedContent
                    _ = codeEditor.InvokeScriptAsync("updateSelectedContent", e.NewValue != null ? e.NewValue.ToString() : string.Empty);
                }

                codeEditor.NotifyPropertyChanged(nameof(SelectedText));
            }
        }));

        /// <summary>
        /// Gets or sets the current primary selection range in the editor.
        /// </summary>
        public Selection SelectedRange
        {
            get => (Selection)GetValue(SelectedRangeProperty);
            set => SetValue(SelectedRangeProperty, value);
        }

        /// <summary>Identifies the <see cref="SelectedRange"/> dependency property.</summary>
        public static DependencyProperty SelectedRangeProperty { get; } = DependencyProperty.Register(nameof(SelectedRange), typeof(Selection), typeof(CodeEditorBase), new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets the syntax language identifier for the editor (e.g., <c>"csharp"</c>,
        /// <c>"javascript"</c>, <c>"xml"</c>).
        /// </summary>
        /// <remarks>
        /// Wraps Monaco <c>editor.setModelLanguage</c>. Changing this property also updates
        /// <see cref="Options"/>.<see cref="StandaloneEditorConstructionOptions.Language"/>.
        /// </remarks>
        public string CodeLanguage
        {
            get => (string)GetValue(CodeLanguageProperty);
            set => SetValue(CodeLanguageProperty, value);
        }

        /// <summary>Identifies the <see cref="CodeLanguage"/> dependency property.</summary>
        public static DependencyProperty CodeLanguageProperty { get; } = DependencyProperty.Register(nameof(CodeLanguage), typeof(string), typeof(CodeEditorBase), new PropertyMetadata("xml", (d, e) =>
        {
            if (d is not CodeEditorBase editor) return;
            editor.Options?.Language = e.NewValue.ToString();
        }));

        /// <summary>
        /// Gets or sets a value indicating whether the editor is in read-only mode.
        /// </summary>
        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        /// <summary>Identifies the <see cref="ReadOnly"/> dependency property.</summary>
        public static DependencyProperty ReadOnlyProperty { get; } = DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(CodeEditorBase), new PropertyMetadata(false, (d, e) =>
        {
            if (d is not CodeEditorBase editor) return;
            editor.Options?.ReadOnly = bool.Parse(e.NewValue?.ToString() ?? "false");
        }));

        /// <summary>
        /// Gets or sets the Monaco editor construction options.
        /// </summary>
        /// <remarks>
        /// Setting this property replaces the entire options object and may overwrite
        /// pass-through properties such as <see cref="CodeLanguage"/> and <see cref="ReadOnly"/>.
        /// Changes to individual properties on the existing <see cref="StandaloneEditorConstructionOptions"/>
        /// instance are automatically forwarded to Monaco via <c>updateOptions</c>.
        /// </remarks>
        public StandaloneEditorConstructionOptions Options
        {
            get => (StandaloneEditorConstructionOptions)GetValue(OptionsProperty);
            set => SetValue(OptionsProperty, value);
        }

        /// <summary>Identifies the <see cref="Options"/> dependency property.</summary>
        public static DependencyProperty OptionsProperty { get; } = DependencyProperty.Register(
            nameof(Options),
            typeof(StandaloneEditorConstructionOptions),
            typeof(CodeEditorBase),
            new PropertyMetadata(
                null,
                (d, e) =>
                {
                    if (d is CodeEditorBase editor)
                    {
                        if (e.OldValue is StandaloneEditorConstructionOptions oldValue)
                            oldValue.PropertyChanged -= editor.Options_PropertyChanged;
                        if (e.NewValue is StandaloneEditorConstructionOptions value)
                        {
                            value.PropertyChanged -= editor.Options_PropertyChanged;
                            value.PropertyChanged += editor.Options_PropertyChanged;
                        }
                    }
                }));

        /// <summary>
        /// Gets or sets a value indicating whether the glyph margin is visible in the editor.
        /// </summary>
        /// <remarks>
        /// The glyph margin is the leftmost column in the editor used to display icons for
        /// breakpoints, bookmarks, and other decorations.
        /// Wraps Monaco <see cref="StandaloneEditorConstructionOptions.GlyphMargin"/>.
        /// </remarks>
        public bool HasGlyphMargin
        {
            get => (bool)GetValue(HasGlyphMarginProperty);
            set => SetValue(HasGlyphMarginProperty, value);
        }

        /// <summary>Identifies the <see cref="HasGlyphMargin"/> dependency property.</summary>
        public static DependencyProperty HasGlyphMarginProperty { get; } = DependencyProperty.Register(nameof(HasGlyphMargin), typeof(bool), typeof(CodeEditorBase), new PropertyMetadata(false, (d, e) =>
        {
            if (d is not CodeEditorBase editor) return;
            editor.Options?.GlyphMargin = e.NewValue as bool?;
        }));

        /// <summary>
        /// Gets or sets text Decorations.
        /// </summary>
        public IObservableVector<IModelDeltaDecoration> Decorations
        {
            get => (IObservableVector<IModelDeltaDecoration>)GetValue(DecorationsProperty);
            set => SetValue(DecorationsProperty, value);
        }

        private readonly AsyncLock _mutexLineDecorations = new();

        private async void Decorations_VectorChanged(IObservableVector<IModelDeltaDecoration> sender, IVectorChangedEventArgs @event)
        {
            if (sender != null)
            {
                // Need to recall mutex as this is called from outside of this initial callback setting it up.
                using (await _mutexLineDecorations.LockAsync())
                {
                    await DeltaDecorationsHelperAsync([.. sender]);
                }
            }
        }

        /// <summary>Identifies the <see cref="Decorations"/> dependency property.</summary>
        public static DependencyProperty DecorationsProperty { get; } = DependencyProperty.Register(nameof(Decorations), typeof(IModelDeltaDecoration), typeof(CodeEditorBase), new PropertyMetadata(null, async (d, e) =>
        {
            if (d is CodeEditorBase editor)
            {
                // We only want to do this one at a time per editor.
                using (await editor._mutexLineDecorations.LockAsync())
                {
                    var old = e.OldValue as IObservableVector<IModelDeltaDecoration>;
                    // Clear out the old line decorations if we're replacing them or setting back to null
                    if ((old != null && old.Count > 0) ||
                             e.NewValue == null)
                    {
                        await editor.DeltaDecorationsHelperAsync([]);
                    }

                    if (e.NewValue is IObservableVector<IModelDeltaDecoration> value)
                    {
                        if (value.Count > 0)
                        {
                            await editor.DeltaDecorationsHelperAsync([.. value]);
                        }

                        value.VectorChanged -= editor.Decorations_VectorChanged;
                        value.VectorChanged += editor.Decorations_VectorChanged;
                    }
                }
            }
        }));

        /// <summary>
        /// Gets or sets the hint Markers.
        /// Note: This property is a helper for <see cref="SetModelMarkersAsync(string, IMarkerData[])"/>; use this property or the method, not both.
        /// </summary>
        public IObservableVector<IMarkerData> Markers
        {
            get => (IObservableVector<IMarkerData>)GetValue(MarkersProperty);
            set => SetValue(MarkersProperty, value);
        }

        private readonly AsyncLock _mutexMarkers = new();

        private async void Markers_VectorChanged(IObservableVector<IMarkerData> sender, IVectorChangedEventArgs @event)
        {
            if (sender != null)
            {
                // Need to recall mutex as this is called from outside of this initial callback setting it up.
                using (await _mutexMarkers.LockAsync())
                {
                    await SetModelMarkersAsync("CodeEditor", [.. sender]);
                }
            }
        }

        /// <summary>Identifies the <see cref="Markers"/> dependency property.</summary>
        public static DependencyProperty MarkersProperty { get; } = DependencyProperty.Register(nameof(Markers), typeof(IMarkerData), typeof(CodeEditorBase), new PropertyMetadata(null, async (d, e) =>
        {
            if (d is CodeEditorBase editor)
            {
                // We only want to do this one at a time per editor.
                using (await editor._mutexMarkers.LockAsync())
                {
                    var old = e.OldValue as IObservableVector<IMarkerData>;
                    // Clear out the old markers if we're replacing them or setting back to null
                    if ((old != null && old.Count > 0) ||
                             e.NewValue == null)
                    {
                        // TODO: Can I simplify this in this case?
                        await editor.SetModelMarkersAsync("CodeEditor", []);
                    }

                    if (e.NewValue is IObservableVector<IMarkerData> value)
                    {
                        if (value.Count > 0)
                        {
                            await editor.SetModelMarkersAsync("CodeEditor", [.. value]);
                        }

                        value.VectorChanged -= editor.Markers_VectorChanged;
                        value.VectorChanged += editor.Markers_VectorChanged;
                    }
                }
            }
        }));
    }
}
