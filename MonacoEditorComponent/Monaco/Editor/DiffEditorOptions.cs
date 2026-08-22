#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Monaco.Editor
{
    /// <summary>
    /// The diff-specific options of a diff editor, bound to
    /// <c>DiffCodeEditor.DiffOptions</c>.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="IDiffEditorBaseOptions"/> rather than the full
    /// <see cref="IDiffEditorOptions"/> union: editor-level options are carried separately by
    /// <see cref="CodeEditorBase.Options"/> and applied to the modified sub-editor, because
    /// Monaco itself has two sinks for them (<c>diffEditor.updateOptions</c> versus
    /// <c>modifiedEditor.updateOptions</c>) and each silently ignores the other's keys.
    /// <para>
    /// Property changes raise <see cref="PropertyChanged"/>, which the control forwards to
    /// Monaco. Only properties set explicitly are serialized, so unset options keep their
    /// Monaco defaults.
    /// </para>
    /// </remarks>
    public sealed class DiffEditorOptions : IDiffEditorBaseOptions, INotifyPropertyChanged
    {
        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly Dictionary<string, object?> _propertyBackingDictionary = [];

        private T? GetPropertyValue<T>([CallerMemberName] string? propertyName = null)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            if (_propertyBackingDictionary.TryGetValue(propertyName, out var value) && value is T ret)
            {
                return ret;
            }

            return default;
        }

        private bool SetPropertyValue<T>(T newValue, [CallerMemberName] string? propertyName = null)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            if (EqualityComparer<T>.Default.Equals(newValue, GetPropertyValue<T>(propertyName))) return false;

            _propertyBackingDictionary[propertyName] = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        /// <summary>
        /// Whether the diff editor aria label should be verbose.
        /// </summary>
        public bool? AccessibilityVerbose { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// If set, the diff editor is optimized for small views.
        /// Defaults to false.
        /// </summary>
        public bool? CompactMode { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Diff algorithm.
        /// Defaults to Advanced.
        /// </summary>
        public DiffAlgorithm? DiffAlgorithm { get => GetPropertyValue<DiffAlgorithm?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Should the diff editor enable code lens?
        /// Defaults to false.
        /// </summary>
        public bool? DiffCodeLens { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Control the wrapping of the diff editor.
        /// </summary>
        public DiffWordWrap? DiffWordWrap { get => GetPropertyValue<DiffWordWrap?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Allow the user to resize the diff editor split view.
        /// Defaults to true.
        /// </summary>
        public bool? EnableSplitViewResizing { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Experimental options. May change between Monaco versions.
        /// </summary>
        public DiffEditorExperimentalOptions? Experimental { get => GetPropertyValue<DiffEditorExperimentalOptions?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Collapse regions that contain no changes.
        /// </summary>
        public DiffEditorHideUnchangedRegionsOptions? HideUnchangedRegions { get => GetPropertyValue<DiffEditorHideUnchangedRegionsOptions?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Compute the diff by ignoring leading/trailing whitespace.
        /// Defaults to true.
        /// </summary>
        public bool? IgnoreTrimWhitespace { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Is the diff editor inside another editor.
        /// Defaults to false.
        /// </summary>
        public bool? IsInEmbeddedEditor { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Timeout in milliseconds after which diff computation is cancelled.
        /// Defaults to 5000.
        /// </summary>
        public uint? MaxComputationTime { get => GetPropertyValue<uint?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Maximum supported file size in MB.
        /// Defaults to 50.
        /// </summary>
        public uint? MaxFileSize { get => GetPropertyValue<uint?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// If the diff editor should only show the difference review mode.
        /// </summary>
        public bool? OnlyShowAccessibleDiffViewer { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Original model should be editable?
        /// Defaults to false.
        /// </summary>
        public bool? OriginalEditable { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Indicates if the gutter menu should be rendered.
        /// </summary>
        public bool? RenderGutterMenu { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Render +/- indicators for added/deleted changes.
        /// Defaults to true.
        /// </summary>
        public bool? RenderIndicators { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Shows icons in the glyph margin to revert changes.
        /// Defaults to true.
        /// </summary>
        public bool? RenderMarginRevertIcon { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Should the diff editor render the overview ruler.
        /// Defaults to true.
        /// </summary>
        public bool? RenderOverviewRuler { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Render the differences in two side-by-side editors.
        /// Defaults to true.
        /// </summary>
        public bool? RenderSideBySide { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// When <see cref="RenderSideBySide"/> is enabled and
        /// <see cref="UseInlineViewWhenSpaceIsLimited"/> is set, a diff editor narrower than this
        /// width (in pixels) renders the inline view instead.
        /// </summary>
        public uint? RenderSideBySideInlineBreakpoint { get => GetPropertyValue<uint?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// The default ratio when rendering side-by-side editors.
        /// Must be a number between 0 and 1; minimum sizes still apply.
        /// Defaults to 0.5.
        /// </summary>
        public double? SplitViewDefaultRatio { get => GetPropertyValue<double?>(); set => SetPropertyValue(value); }

        /// <summary>
        /// Switch to the inline view when the diff editor is narrower than
        /// <see cref="RenderSideBySideInlineBreakpoint"/>.
        /// </summary>
        public bool? UseInlineViewWhenSpaceIsLimited { get => GetPropertyValue<bool?>(); set => SetPropertyValue(value); }
    }
}
