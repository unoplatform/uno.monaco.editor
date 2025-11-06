using Microsoft.UI.Xaml;

using Monaco.Editor;
using Monaco.Helpers;

using Nito.AsyncEx;

using Windows.Foundation.Collections;

namespace Monaco
{
    partial class CodeEditor : IParentAccessorAcceptor
    {
        /// <summary>
        /// Construct to help with syntax for accessing Monaco.Language.* APIs.
        /// </summary>
        public LanguagesHelper Languages { get; }

        public bool IsSettingValue { get; set; }

        /// <summary>
        /// Get or Set the CodeEditor Text.
        /// </summary>
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static DependencyProperty TextProperty { get; } = DependencyProperty.Register(nameof(Text), typeof(string), typeof(CodeEditor), new PropertyMetadata(string.Empty, async (d, e) =>
        {
            if (d is CodeEditor codeEditor)
            {
                if (!codeEditor.IsSettingValue)
                {
                    codeEditor.QueueOrExecutePropertyChange(async () =>
                    {
                        // link:otherScriptsToBeOrganized.ts:updateContent
                        await codeEditor.InvokeScriptAsync("updateContent", e.NewValue?.ToString() ?? string.Empty);
                        codeEditor.NotifyPropertyChanged(nameof(Text));
                    }, CodeEditor.PRIORITY_CONTENT); // Text should be set after language
                }
                else
                {
                    codeEditor.NotifyPropertyChanged(nameof(Text));
                }
            }
        }));

        /// <summary>
        /// Get the current Primary Selected CodeEditor Text.
        /// </summary>
        public string SelectedText
        {
            get => (string)GetValue(SelectedTextProperty);
            set => SetValue(SelectedTextProperty, value);
        }

        public static DependencyProperty SelectedTextProperty { get; } = DependencyProperty.Register(nameof(SelectedText), typeof(string), typeof(CodeEditor), new PropertyMetadata(string.Empty, (d, e) =>
        {
            if (d is CodeEditor codeEditor)
            {
                if (!codeEditor.IsSettingValue)
                {
                    codeEditor.QueueOrExecutePropertyChange(async () =>
                    {
                        // link:updateSelectedContent.ts:updateSelectedContent
                        await codeEditor.InvokeScriptAsync("updateSelectedContent", e.NewValue?.ToString() ?? string.Empty);
                        codeEditor.NotifyPropertyChanged(nameof(SelectedText));
                    }, CodeEditor.PRIORITY_CONTENT);
                }
                else
                {
                    codeEditor.NotifyPropertyChanged(nameof(SelectedText));
                }
            }
        }));

        public Selection SelectedRange
        {
            get => (Selection)GetValue(SelectedRangeProperty);
            set => SetValue(SelectedRangeProperty, value);
        }

        public static DependencyProperty SelectedRangeProperty { get; } = DependencyProperty.Register(nameof(SelectedRange), typeof(Selection), typeof(CodeEditor), new PropertyMetadata(null));

        /// <summary>
        /// Set the Syntax Language for the Code CodeEditor.
        /// 
        /// Note: Most likely to change or move location.
        /// </summary>
        public string CodeLanguage
        {
            get => (string)GetValue(CodeLanguageProperty);
            set => SetValue(CodeLanguageProperty, value);
        }

        public static DependencyProperty CodeLanguageProperty { get; } = DependencyProperty.Register(nameof(CodeLanguage), typeof(string), typeof(CodeEditor), new PropertyMetadata("xml", (d, e) =>
        {
            if (d is not CodeEditor editor) return;
            var language = e.NewValue?.ToString();
            if (editor.Options != null)
            {
                editor.Options.Language = language;
            }
            editor.QueueOrExecutePropertyChange(async () =>
            {
                if (language != null)
                {
                    await editor.InvokeScriptAsync("updateLanguage", language);
                }
            }, CodeEditor.PRIORITY_OPTIONS); // Language must be set before content
        }));

        /// <summary>
        /// Set the ReadOnly option for the Code CodeEditor.
        /// </summary>
        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public static DependencyProperty ReadOnlyProperty { get; } = DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(CodeEditor), new PropertyMetadata(false, (d, e) =>
        {
            if (d is not CodeEditor editor) return;
            var readOnly = bool.Parse(e.NewValue?.ToString() ?? "false");
            if (editor.Options != null)
            {
                editor.Options.ReadOnly = readOnly;
            }
            editor.QueueOrExecutePropertyChange(async () =>
            {
                await editor.InvokeScriptAsync("updateOptions", editor.Options);
            }, CodeEditor.PRIORITY_OPTIONS);
        }));

        /// <summary>
        /// Get or set the CodeEditor Options. Node: Will overwrite CodeLanguage.
        /// </summary>
        public StandaloneEditorConstructionOptions Options
        {
            get => (StandaloneEditorConstructionOptions)GetValue(OptionsProperty);
            set => SetValue(OptionsProperty, value);
        }

        public static DependencyProperty OptionsProperty { get; } = DependencyProperty.Register(
            nameof(Options),
            typeof(StandaloneEditorConstructionOptions),
            typeof(CodeEditor),
            new PropertyMetadata(
                null,
                (d, e) =>
                {
                    if (d is CodeEditor editor)
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
        /// Get or Set the CodeEditor Text.
        /// </summary>
        public bool HasGlyphMargin
        {
            get => (bool)GetValue(HasGlyphMarginProperty);
            set => SetValue(HasGlyphMarginProperty, value);
        }

        public static DependencyProperty HasGlyphMarginProperty { get; } = DependencyProperty.Register(nameof(HasGlyphMargin), typeof(bool), typeof(CodeEditor), new PropertyMetadata(false, (d, e) =>
        {
            if (d is not CodeEditor editor) return;
            var glyphMargin = e.NewValue as bool?;
            if (editor.Options != null)
            {
                editor.Options.GlyphMargin = glyphMargin;
            }
            editor.QueueOrExecutePropertyChange(async () =>
            {
                await editor.InvokeScriptAsync("updateOptions", editor.Options);
            }, CodeEditor.PRIORITY_OPTIONS);
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
                QueueOrExecutePropertyChange(async () =>
                {
                    // Need to recall mutex as this is called from outside of this initial callback setting it up.
                    using (await _mutexLineDecorations.LockAsync())
                    {
                        await DeltaDecorationsHelperAsync([.. sender]);
                    }
                }, CodeEditor.PRIORITY_DECORATIONS);
            }
        }

        public static DependencyProperty DecorationsProperty { get; } = DependencyProperty.Register(nameof(Decorations), typeof(IModelDeltaDecoration), typeof(CodeEditor), new PropertyMetadata(null, async (d, e) =>
        {
            if (d is CodeEditor editor)
            {
                editor.QueueOrExecutePropertyChange(async () =>
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
                }, CodeEditor.PRIORITY_DECORATIONS);
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
                QueueOrExecutePropertyChange(async () =>
                {
                    // Need to recall mutex as this is called from outside of this initial callback setting it up.
                    using (await _mutexMarkers.LockAsync())
                    {
                        await SetModelMarkersAsync("CodeEditor", [.. sender]);
                    }
                }, CodeEditor.PRIORITY_DECORATIONS);
            }
        }

        public static DependencyProperty MarkersProperty { get; } = DependencyProperty.Register(nameof(Markers), typeof(IMarkerData), typeof(CodeEditor), new PropertyMetadata(null, async (d, e) =>
        {
            if (d is CodeEditor editor)
            {
                editor.QueueOrExecutePropertyChange(async () =>
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
                }, CodeEditor.PRIORITY_DECORATIONS);
            }
        }));
    }
}
