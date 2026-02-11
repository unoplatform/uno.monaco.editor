using System.Text.Json;

using CommunityToolkit.WinUI;

using Monaco.Editor;
using Monaco.Extensions;
using Monaco.Serialization;

using Windows.Foundation;

namespace Monaco
{
    /// <summary>
    /// Action delegate for <see cref="CodeEditor.AddCommandAsync(int, CommandHandler)"/> and <see cref="CodeEditor.AddCommandAsync(int, CommandHandler, string)"/>.
    /// </summary>
    public delegate void CommandHandler(object?[] parameters);

    /// <summary>
    /// This file contains Monaco IEditor method implementations we can call on our control.
    /// https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.ieditor.html
    /// https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.icommoncodeeditor.html
    /// </summary>
    public partial class CodeEditor
    {
        #region Reveal Methods
        public IAsyncAction RevealLineAsync(uint lineNumber)
        {
            return SendScriptAsync("editor.revealLine(" + lineNumber + ")").AsAsyncAction();
        }

        public IAsyncAction RevealLineInCenterAsync(uint lineNumber)
        {
            return SendScriptAsync("editor.revealLineInCenter(" + lineNumber + ")").AsAsyncAction();
        }

        public IAsyncAction RevealLineInCenterIfOutsideViewportAsync(uint lineNumber)
        {
            return SendScriptAsync("editor.revealLineInCenterIfOutsideViewport(" + lineNumber + ")").AsAsyncAction();
        }

        public IAsyncAction RevealLinesAsync(uint startLineNumber, uint endLineNumber)
        {
            return SendScriptAsync("editor.revealLines(" + startLineNumber + ", " + endLineNumber + ")").AsAsyncAction();
        }

        public IAsyncAction RevealLinesInCenterAsync(uint startLineNumber, uint endLineNumber)
        {
            return SendScriptAsync("editor.revealLinesInCenter(" + startLineNumber + ", " + endLineNumber + ")").AsAsyncAction();
        }

        public IAsyncAction RevealLinesInCenterIfOutsideViewportAsync(uint startLineNumber, uint endLineNumber)
        {
            return SendScriptAsync("editor.revealLinesInCenterIfOutsideViewport(" + startLineNumber + ", " + endLineNumber + ")").AsAsyncAction();
        }

        public IAsyncAction RevealPositionAsync(IPosition position)
        {
            return RevealPositionAsync(position, false, false);
        }

        public IAsyncAction RevealPositionAsync(IPosition position, bool revealVerticalInCenter)
        {
            return RevealPositionAsync(position, revealVerticalInCenter, false);
        }

        public IAsyncAction RevealPositionAsync(IPosition position, bool revealVerticalInCenter, bool revealHorizontal)
        {
            return SendScriptAsync("editor.revealPosition(JSON.parse('" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + "'), " + JsonSerializer.Serialize(revealVerticalInCenter) + ", " + JsonSerializer.Serialize(revealHorizontal) + ")").AsAsyncAction();
        }

        public IAsyncAction RevealPositionInCenterAsync(IPosition position)
        {
            return SendScriptAsync("editor.revealPositionInCenter(JSON.parse('" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + "'))").AsAsyncAction();
        }

        public IAsyncAction RevealPositionInCenterIfOutsideViewportAsync(IPosition position)
        {
            return SendScriptAsync("editor.revealPositionInCenterIfOutsideViewport(JSON.parse('" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + "'))").AsAsyncAction();
        }

        public IAsyncAction RevealRangeAsync(IRange range)
        {
            return SendScriptAsync("editor.revealRange(JSON.parse('" + JsonSerializer.Serialize(Range.Lift(range), MonacoJsonContext.Relaxed.Range) + "'))").AsAsyncAction();
        }

        public IAsyncAction RevealRangeAtTopAsync(IRange range)
        {
            return SendScriptAsync("editor.revealRangeAtTop(JSON.parse('" + JsonSerializer.Serialize(Range.Lift(range), MonacoJsonContext.Relaxed.Range) + "'))").AsAsyncAction();
        }

        public IAsyncAction RevealRangeInCenterAsync(IRange range)
        {
            return SendScriptAsync("editor.revealRangeInCenter(JSON.parse('" + JsonSerializer.Serialize(Range.Lift(range), MonacoJsonContext.Relaxed.Range) + "'))").AsAsyncAction();
        }

        public IAsyncAction RevealRangeInCenterIfOutsideViewportAsync(IRange range)
        {
            return SendScriptAsync("editor.revealRangeInCenterIfOutsideViewport(JSON.parse('" + JsonSerializer.Serialize(Range.Lift(range), MonacoJsonContext.Relaxed.Range) + "'))").AsAsyncAction();
        }
        #endregion

        public IAsyncAction AddActionAsync(IActionDescriptor action)
        {
            if (_parentAccessor is null)
            {
                if (!OperatingSystem.IsBrowser())
                {
                    throw new PlatformNotSupportedException(
                        "AddActionAsync is not yet supported on desktop. Desktop bridge helpers will be available in a future update.");
                }

                throw new InvalidOperationException("_parentAccessor is not available");
            }

            var wref = new WeakReference<CodeEditor>(this);
            _parentAccessor.RegisterAction("Action" + action.Id, new Action(() => { if (wref.TryGetTarget(out var editor)) { action?.Run(editor, null); } }));
            return InvokeScriptAsync("addAction", action).AsAsyncAction();
        }

        /// <summary>
        /// Invoke scripts, return value must be strings
        /// </summary>
        /// <param name="script">Script to invoke</param>
        /// <returns>An async operation result to string</returns>
        public async Task<string?> InvokeScriptAsync(string script)
        {
            if (_view is not null)
            {
                var r = await _view.InvokeScriptAsync("eval", [script]);
                return r?.ToString();
            }

            return null;
        }

        private int _commandIndex = 0;

        public async Task<string?> AddCommandAsync(CommandHandler handler)
        {
            return await AddCommandAsync(0, handler, string.Empty);
        }
        public async Task<string?> AddCommandAsync(int keybinding, CommandHandler handler)
        {
            return await AddCommandAsync(keybinding, handler, string.Empty);
        }

        public async Task<string?> AddCommandAsync(int keybinding, CommandHandler handler, string context)
        {
            if (_parentAccessor is null)
            {
                if (!OperatingSystem.IsBrowser())
                {
                    throw new PlatformNotSupportedException(
                        "AddCommandAsync is not yet supported on desktop. Desktop bridge helpers will be available in a future update.");
                }

                throw new InvalidOperationException("_parentAccessor is not available");
            }

            var name = "Command" + Interlocked.Increment(ref _commandIndex);
            _parentAccessor.RegisterActionWithParameters(name, (parameters) =>
            {
                if (parameters != null && parameters.Length > 0)
                {
                    // Breaking change: returns JsonElement instead of JObject.
                    // Consumers should use JsonElement API (GetProperty, GetString, etc.).
                    object?[] args = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        args[i] = JsonSerializer.Deserialize<object>(parameters[i], MonacoJsonContext.Default.Options);
                    }

                    handler?.Invoke(args);
                }
                else
                {
                    handler?.Invoke([]);
                }
            });
            return await InvokeScriptAsync<string>("addCommand", [keybinding, name, context]);
        }

        public async Task<ContextKey> CreateContextKeyAsync(string key, bool defaultValue)
        {
            var ck = new ContextKey(this, key, defaultValue);

            await InvokeScriptAsync("createContext", ck);

            return ck;
        }

        public IModel? GetModel()
        {
            return _model;
        }

        public async Task<IEnumerable<Marker?>> GetModelMarkersAsync() // TODO: Filter (string? owner, Uri? resource, int? take)
        {
            return await SendScriptAsync<IEnumerable<Marker>>("monaco.editor.getModelMarkers();").AsAsyncOperation();
        }

        public async Task SetModelMarkersAsync(string owner, IMarkerData[] markers)
        {
            await SendScriptAsync("monaco.editor.setModelMarkers(EditorContext.getEditorForElement(element).model, " + JsonSerializer.Serialize(owner, MonacoJsonContext.Relaxed.Options) + ", " + JsonSerializer.Serialize(markers, MonacoJsonContext.Relaxed.Options) + ");").AsAsyncAction();
        }

        public async Task<Position?> GetPositionAsync()
        {
            return await SendScriptAsync<Position>("EditorContext.getEditorForElement(element).editor.getPosition();").AsAsyncOperation();
        }

        public IAsyncAction SetPositionAsync(IPosition position)
        {
            return SendScriptAsync("EditorContext.getEditorForElement(element).editor.setPosition(" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + ");").AsAsyncAction();
        }

        /// <summary>
        /// https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.icommoncodeeditor.html#deltadecorations
        /// 
        /// Using <see cref="Decorations"/> Property to manipulate decorations instead of calling this directly.
        /// </summary>
        /// <param name="newDecorations"></param>
        /// <returns></returns>
        private async Task DeltaDecorationsHelperAsync(IModelDeltaDecoration[] newDecorations)
        {
            _queue = _queue ?? throw new InvalidOperationException($"_queue is not available");

            await _queue.EnqueueAsync(async () =>
            {
                var newDecorationsAdjust = newDecorations ?? [];

                if (_cssBroker is not null
                    && _cssBroker.AssociateStyles(newDecorationsAdjust))
                {
                    // Update Styles First
                    await InvokeScriptAsync("updateStyle", _cssBroker.GetStyles());
                }

                // Send Command to Modify Decorations
                // IMPORTANT: Need to cast to object here as we want this to be a single array object passed as a parameter, not a list of parameters to expand.
                await InvokeScriptAsync("updateDecorations", (object)newDecorationsAdjust);
            });
        }
    }
}
