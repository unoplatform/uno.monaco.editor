using System.ComponentModel;
using System.Text.Json;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

using Monaco.Editor;
using Monaco.Helpers;
using Monaco.Serialization;

using Windows.Foundation;

namespace Monaco
{
    /// <summary>
    /// A two-sided comparison view built on the Monaco
    /// <see href="https://microsoft.github.io/monaco-editor/typedoc/interfaces/editor.IDiffEditor.html">diff editor</see>.
    /// </summary>
    /// <remarks>
    /// Shares the host, lifecycle, and bridge with <see cref="CodeEditor"/> through
    /// <see cref="CodeEditorBase"/>; only the bootstrap entry point and the text properties
    /// differ.
    /// <para>
    /// The modified (right-hand) document is the editable one, and it is what the inherited
    /// members act on: <see cref="CodeEditorBase.SelectedText"/>,
    /// <see cref="CodeEditorBase.Decorations"/>, <see cref="CodeEditorBase.Markers"/>,
    /// <see cref="CodeEditorBase.Options"/>, cursor position, and actions and commands all
    /// target it. Diff-specific configuration lives on <see cref="DiffOptions"/> instead,
    /// because Monaco keeps the two option sets in separate sinks.
    /// </para>
    /// </remarks>
    public sealed partial class DiffCodeEditor : CodeEditorBase
    {
        /// <summary>
        /// Occurs when Monaco finishes recomputing the diff, whether because either
        /// document changed or because a diff option changed.
        /// </summary>
        /// <remarks>
        /// Pairs with <see cref="GetLineChangesAsync"/>: this event says the diff changed,
        /// that method says what it became. Before the first occurrence
        /// <see cref="GetLineChangesAsync"/> reports an empty set, which is indistinguishable
        /// from two identical documents -- so treat this event, not the return value, as the
        /// signal that a diff has actually been computed.
        /// </remarks>
        public event TypedEventHandler<DiffCodeEditor, EventArgs>? DiffUpdated;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiffCodeEditor"/> class on the current UI thread.
        /// </summary>
        public DiffCodeEditor() : this(null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DiffCodeEditor"/> class with an explicit dispatcher.
        /// </summary>
        /// <param name="queue">
        /// The <see cref="DispatcherQueue"/> for the UI thread. When <see langword="null"/>, the
        /// current thread's dispatcher is used.
        /// </param>
        public DiffCodeEditor(DispatcherQueue? queue) : base(queue)
        {
            DefaultStyleKey = typeof(DiffCodeEditor);

            if (ReadLocalValue(DiffOptionsProperty) == DependencyProperty.UnsetValue)
            {
                DiffOptions = new DiffEditorOptions();
            }

            // CodeLanguage forwarding on the base runs through Options.Language and reaches
            // only the modified model. When OriginalLanguage is unset the original side is
            // supposed to follow CodeLanguage, so it has to be pushed here as well --
            // otherwise switching language at runtime re-tokenizes only the right-hand side.
            // No token is kept: the callback targets this same control, so it dies with it.
            RegisterPropertyChangedCallback(CodeLanguageProperty, OnCodeLanguageChanged);
        }

        private async void OnCodeLanguageChanged(DependencyObject sender, DependencyProperty property)
        {
            // An explicit OriginalLanguage wins; its own DP callback already pushes it.
            if (!IsEditorLoaded || !string.IsNullOrEmpty(OriginalLanguage))
            {
                return;
            }

            await InvokeScriptAsync("updateOriginalLanguage", EffectiveOriginalLanguage);
        }

        /// <inheritdoc />
        protected override string BootstrapFunctionName => "createMonacoDiffEditor";

        /// <inheritdoc />
        protected internal override bool IsDiffEditor => true;

        /// <inheritdoc />
        protected override string? PrimaryText => ModifiedText;

        /// <summary>
        /// The language applied to the original document: <see cref="OriginalLanguage"/> when
        /// set, otherwise the same language as the modified side.
        /// </summary>
        private string EffectiveOriginalLanguage =>
            string.IsNullOrEmpty(OriginalLanguage) ? CodeLanguage : OriginalLanguage;

        /// <inheritdoc />
        private protected override void RegisterBridgeCallbacks(IParentAccessor? accessor)
        {
            base.RegisterBridgeCallbacks(accessor);

            // Routed through the already-allowlisted parentAccessor/callAction, so the diff
            // editor adds no new JSON-RPC method to the desktop bridge.
            accessor?.RegisterAction("DiffUpdated", () => DiffUpdated?.Invoke(this, EventArgs.Empty));
        }

        /// <inheritdoc />
        protected override Dictionary<string, object?> BuildInitialStateMap()
        {
            var state = base.BuildInitialStateMap();

            state["originalText"] = OriginalText ?? string.Empty;
            state["originalLanguage"] = EffectiveOriginalLanguage ?? "plaintext";

            // Serialized through the source-generated context rather than left to the
            // reflection-based fallback that writes the outer map.
            state["diffOptions"] = DiffOptions is null
                ? null
                : JsonSerializer.SerializeToElement(DiffOptions, MonacoJsonContext.Default.DiffEditorOptions);

            return state;
        }

        /// <inheritdoc />
        protected override async Task ApplyInitialPropertyValues()
        {
            // The base applies language, options, theme, and the modified document.
            await base.ApplyInitialPropertyValues();

            if (!string.IsNullOrEmpty(EffectiveOriginalLanguage))
            {
                await InvokeScriptAsync("updateOriginalLanguage", EffectiveOriginalLanguage);
            }

            await InvokeScriptAsync("updateOriginalContent", OriginalText ?? string.Empty);

            if (DiffOptions is not null)
            {
                await InvokeScriptAsync("updateDiffOptions", DiffOptions);
            }
        }

        private async void DiffOptions_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not DiffEditorOptions options)
            {
                return;
            }

            await InvokeScriptAsync("updateDiffOptions", options);
        }
    }
}
