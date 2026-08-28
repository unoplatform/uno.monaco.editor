using System.ComponentModel;
using System.Text.Json;

using Collections.Generic;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

using Monaco.Editor;
using Monaco.Helpers;
using Monaco.Serialization;

using Nito.AsyncEx;

using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Monaco
{
    /// <summary>
    /// A scrollable list of per-file diffs, equivalent to VS Code's multi-file diff editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built on Monaco's own multi-diff widget, so it inherits its behaviour: one shared vertical
    /// scroll across every file, sticky collapsible headers, virtualization (only the visible
    /// files have live editors), <c>A</c>/<c>D</c>/<c>R</c> badges for added, deleted and renamed
    /// files, and unchanged regions folded away inside each file.
    /// </para>
    /// <para>
    /// <b>Read-only.</b> Every file is locked on both sides. The <see cref="Files"/> collection is
    /// the only way content changes.
    /// </para>
    /// <para>
    /// <b>The inherited single-document members do not apply here.</b>
    /// <see cref="EditorHostBase.SelectedText"/>, <see cref="EditorHostBase.SelectedRange"/>,
    /// <see cref="EditorHostBase.CodeLanguage"/>, <see cref="EditorHostBase.ReadOnly"/>,
    /// <see cref="EditorHostBase.Options"/>, <see cref="EditorHostBase.HasGlyphMargin"/>,
    /// <see cref="EditorHostBase.Decorations"/>, <see cref="EditorHostBase.Markers"/>, the cursor
    /// position accessors, and the action and command APIs are all inert: Monaco pools and recycles
    /// the per-file editors, so there is no stable single editor for them to act on. Set the
    /// language per file with <see cref="DiffFileEntry.Language"/> instead, and configure the
    /// comparison through <see cref="DiffOptions"/>.
    /// </para>
    /// </remarks>
    public sealed partial class MultiDiffCodeEditor : EditorHostBase
    {
        // One push at a time, so a burst of collection and per-entry changes cannot interleave
        // into an out-of-order file list. Mirrors the Decorations/Markers idiom on the base.
        private readonly AsyncLock _mutexFiles = new();

        /// <summary>
        /// Occurs when Monaco finishes recomputing the diff for any file.
        /// </summary>
        public event TypedEventHandler<MultiDiffCodeEditor, EventArgs>? DiffUpdated;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiDiffCodeEditor"/> class on the current UI thread.
        /// </summary>
        public MultiDiffCodeEditor() : this(null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiDiffCodeEditor"/> class with an explicit dispatcher.
        /// </summary>
        /// <param name="queue">
        /// The <see cref="DispatcherQueue"/> for the UI thread. When <see langword="null"/>, the
        /// current thread's dispatcher is used.
        /// </param>
        public MultiDiffCodeEditor(DispatcherQueue? queue) : base(queue)
        {
            DefaultStyleKey = typeof(MultiDiffCodeEditor);

            if (ReadLocalValue(DiffOptionsProperty) == DependencyProperty.UnsetValue)
            {
                DiffOptions = new DiffEditorOptions();
            }

            if (ReadLocalValue(FilesProperty) == DependencyProperty.UnsetValue)
            {
                Files = new ObservableVector<DiffFileEntry>();
            }
        }

        /// <inheritdoc />
        protected override string BootstrapFunctionName => "createMonacoMultiDiffEditor";

        /// <inheritdoc />
        internal override EditorFlavor Flavor => EditorFlavor.MultiDiff;

        /// <inheritdoc />
        /// <remarks>
        /// There is no primary document: this control renders N of them, and none of the
        /// inherited single-document pushes have a target.
        /// </remarks>
        protected override bool HasPrimaryDocument => false;

        /// <inheritdoc />
        protected override string? PrimaryText => null;

        /// <inheritdoc />
        private protected override void RegisterBridgeCallbacks(IParentAccessor? accessor)
        {
            base.RegisterBridgeCallbacks(accessor);

            // Routed through the already-allowlisted parentAccessor/callAction and
            // callActionWithParameters, so the multi-file control adds no new JSON-RPC method to
            // the desktop bridge -- the same arrangement DiffCodeEditor uses for DiffUpdated.
            accessor?.RegisterAction("DiffUpdated", () => DiffUpdated?.Invoke(this, EventArgs.Empty));
            accessor?.RegisterActionWithParameters("MultiDiffActiveFileChanged", OnActiveFileChangedFromJs);
            accessor?.RegisterActionWithParameters("MultiDiffFileCollapsedChanged", OnFileCollapsedChangedFromJs);
        }

        private void OnActiveFileChangedFromJs(string[] parameters)
        {
            var path = parameters is { Length: > 0 } ? parameters[0] : null;
            ActiveFilePath = string.IsNullOrEmpty(path) ? null : path;
        }

        private void OnFileCollapsedChangedFromJs(string[] parameters)
        {
            if (parameters is not { Length: >= 2 })
            {
                return;
            }

            var entry = FindFile(parameters[0]);
            if (entry is null || !bool.TryParse(parameters[1], out var collapsed))
            {
                return;
            }

            // Guarded so the write-back does not bounce straight into another push. The entry's
            // PropertyChanged would otherwise re-send the whole list for a state JS already has.
            _isApplyingJsFileState = true;
            try
            {
                entry.Collapsed = collapsed;
            }
            finally
            {
                _isApplyingJsFileState = false;
            }
        }

        private bool _isApplyingJsFileState;

        private DiffFileEntry? FindFile(string path)
        {
            if (Files is null)
            {
                return null;
            }

            foreach (var entry in Files)
            {
                if (string.Equals(entry.Path, path, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        /// <inheritdoc />
        protected override Dictionary<string, object?> BuildInitialStateMap()
        {
            var state = base.BuildInitialStateMap();

            // Pre-serialized through the source-generated context, because the outer map goes
            // through the reflection fallback -- the same reason DiffCodeEditor pre-serializes
            // diffOptions.
            state["files"] = JsonSerializer.SerializeToElement(SnapshotFiles(), MonacoJsonContext.Default.DiffFileEntryArray);
            state["diffOptions"] = DiffOptions is null
                ? null
                : JsonSerializer.SerializeToElement(DiffOptions, MonacoJsonContext.Default.DiffEditorOptions);

            return state;
        }

        /// <inheritdoc />
        protected override async Task ApplyInitialPropertyValues()
        {
            // The base applies the theme and, because HasPrimaryDocument is false, stops there.
            await base.ApplyInitialPropertyValues();

            if (DiffOptions is not null)
            {
                await InvokeScriptAsync("updateMultiDiffOptions", DiffOptions);
            }

            await PushFilesAsync();
        }

        private DiffFileEntry[] SnapshotFiles() => Files is null ? [] : [.. Files];

        /// <summary>
        /// Re-push the whole file list. Cheap by design: the JavaScript side reconciles by path,
        /// keeping the same document objects for unchanged files, so scroll and collapsed state
        /// survive. That is what lets every kind of change take this one code path.
        /// </summary>
        private async Task PushFilesAsync()
        {
            using (await _mutexFiles.LockAsync())
            {
                // The (string, object?) cast is load-bearing. DiffFileEntry[] is assignable to
                // object[], so without it the compiler picks InvokeScriptAsync(string, object[]),
                // which treats every file as a *separate* JS argument -- the helper then receives
                // only the first one and throws while iterating it, and InvokeScriptAsync swallows
                // that into InternalException. Desktop hid the bug because its file list also
                // arrives in the pushed initial state; WASM, which has no pushed state, simply
                // rendered "No Changed Files" forever.
                // link:multiDiffEditor.ts:updateMultiDiffFiles
                await InvokeScriptAsync("updateMultiDiffFiles", (object)SnapshotFiles());
            }
        }

        private async void OnFilesChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is IObservableVector<DiffFileEntry> oldValue)
            {
                oldValue.VectorChanged -= Files_VectorChanged;
            }

            // Unsubscribe through the same bookkeeping that subscribed, not by walking the old
            // vector: an entry present in both collections would otherwise be detached here and
            // then skipped by SyncFileSubscriptions, because _subscribedFiles still claims it.
            // Editing that entry would silently stop reaching Monaco.
            foreach (var entry in _subscribedFiles)
            {
                entry.PropertyChanged -= File_PropertyChanged;
            }

            _subscribedFiles.Clear();

            if (e.NewValue is IObservableVector<DiffFileEntry> value)
            {
                value.VectorChanged -= Files_VectorChanged;
                value.VectorChanged += Files_VectorChanged;
                SyncFileSubscriptions(value);
            }

            if (IsEditorLoaded)
            {
                await PushFilesAsync();
            }
        }

        private async void Files_VectorChanged(IObservableVector<DiffFileEntry> sender, IVectorChangedEventArgs @event)
        {
            // Resubscribe wholesale rather than tracking the delta: the list is small, and a
            // missed unsubscribe leaks an entry that keeps pushing after it has been removed.
            SyncFileSubscriptions(sender);

            if (IsEditorLoaded)
            {
                await PushFilesAsync();
            }
        }

        private async void File_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Collapse state that JS just reported is already applied there; echoing it back
            // would push the whole list for nothing.
            if (_isApplyingJsFileState)
            {
                return;
            }

            if (IsEditorLoaded)
            {
                await PushFilesAsync();
            }
        }

        private readonly HashSet<DiffFileEntry> _subscribedFiles = [];

        private void SyncFileSubscriptions(IObservableVector<DiffFileEntry> files)
        {
            var current = new HashSet<DiffFileEntry>(files);

            foreach (var entry in _subscribedFiles)
            {
                if (!current.Contains(entry))
                {
                    entry.PropertyChanged -= File_PropertyChanged;
                }
            }

            foreach (var entry in current)
            {
                if (_subscribedFiles.Add(entry))
                {
                    entry.PropertyChanged += File_PropertyChanged;
                }
            }

            _subscribedFiles.IntersectWith(current);
        }

        private async void OnDiffOptionsChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DiffEditorOptions oldValue)
            {
                oldValue.PropertyChanged -= DiffOptions_PropertyChanged;
            }

            if (e.NewValue is not DiffEditorOptions value)
            {
                return;
            }

            value.PropertyChanged -= DiffOptions_PropertyChanged;
            value.PropertyChanged += DiffOptions_PropertyChanged;

            if (IsEditorLoaded)
            {
                // link:multiDiffEditor.ts:updateMultiDiffOptions
                await InvokeScriptAsync("updateMultiDiffOptions", value);
            }
        }

        private async void DiffOptions_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Gated the same way DiffCodeEditor gates its own options push: anything skipped
            // while the editor is down is re-sent wholesale by ApplyInitialPropertyValues.
            if (sender is DiffEditorOptions options && IsEditorLoaded)
            {
                await InvokeScriptAsync("updateMultiDiffOptions", options);
            }
        }
    }
}
