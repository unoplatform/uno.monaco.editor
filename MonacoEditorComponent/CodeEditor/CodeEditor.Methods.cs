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
    /// Contains Monaco <c>IEditor</c> method implementations exposed as async helpers on the control.
    /// </summary>
    /// <remarks>
    /// See <see href="https://microsoft.github.io/monaco-editor/typedoc/interfaces/editor_editor_api.editor.ICodeEditor.html">ICodeEditor</see>
    /// for the upstream Monaco API surface.
    /// </remarks>
    public partial class CodeEditor
    {
        #region Reveal Methods
        /// <summary>
        /// Scrolls the editor to reveal the specified line.
        /// </summary>
        /// <param name="lineNumber">The 1-based line number to reveal.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealLineAsync(uint lineNumber)
        {
            return SendScriptAsync("editor.revealLine(" + lineNumber + ")").AsAsyncAction();
        }

        /// <summary>Scrolls the editor to reveal the specified line in the center of the viewport.</summary>
        /// <param name="lineNumber">The 1-based line number to reveal.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealLineInCenterAsync(uint lineNumber)
        {
            return SendScriptAsync("editor.revealLineInCenter(" + lineNumber + ")").AsAsyncAction();
        }

        /// <summary>Scrolls the editor to reveal the specified line in the center, only if it is outside the current viewport.</summary>
        /// <param name="lineNumber">The 1-based line number to reveal.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealLineInCenterIfOutsideViewportAsync(uint lineNumber)
        {
            return SendScriptAsync("editor.revealLineInCenterIfOutsideViewport(" + lineNumber + ")").AsAsyncAction();
        }

        /// <summary>Scrolls the editor to reveal the specified range of lines.</summary>
        /// <param name="startLineNumber">The 1-based start line number.</param>
        /// <param name="endLineNumber">The 1-based end line number.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealLinesAsync(uint startLineNumber, uint endLineNumber)
        {
            return SendScriptAsync("editor.revealLines(" + startLineNumber + ", " + endLineNumber + ")").AsAsyncAction();
        }

        /// <summary>Scrolls the editor to reveal the specified range of lines in the center of the viewport.</summary>
        /// <param name="startLineNumber">The 1-based start line number.</param>
        /// <param name="endLineNumber">The 1-based end line number.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealLinesInCenterAsync(uint startLineNumber, uint endLineNumber)
        {
            return SendScriptAsync("editor.revealLinesInCenter(" + startLineNumber + ", " + endLineNumber + ")").AsAsyncAction();
        }

        /// <summary>Scrolls the editor to reveal the specified range of lines in the center, only if outside the viewport.</summary>
        /// <param name="startLineNumber">The 1-based start line number.</param>
        /// <param name="endLineNumber">The 1-based end line number.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealLinesInCenterIfOutsideViewportAsync(uint startLineNumber, uint endLineNumber)
        {
            return SendScriptAsync("editor.revealLinesInCenterIfOutsideViewport(" + startLineNumber + ", " + endLineNumber + ")").AsAsyncAction();
        }

        /// <summary>Scrolls the editor to reveal the specified position.</summary>
        /// <param name="position">The position to reveal.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealPositionAsync(IPosition position)
        {
            return RevealPositionAsync(position, false, false);
        }

        /// <summary>Scrolls the editor to reveal the specified position.</summary>
        /// <param name="position">The position to reveal.</param>
        /// <param name="revealVerticalInCenter">When <see langword="true"/>, centers the position vertically.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealPositionAsync(IPosition position, bool revealVerticalInCenter)
        {
            return RevealPositionAsync(position, revealVerticalInCenter, false);
        }

        /// <summary>Scrolls the editor to reveal the specified position.</summary>
        /// <param name="position">The position to reveal.</param>
        /// <param name="revealVerticalInCenter">When <see langword="true"/>, centers the position vertically.</param>
        /// <param name="revealHorizontal">When <see langword="true"/>, also scrolls horizontally.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealPositionAsync(IPosition position, bool revealVerticalInCenter, bool revealHorizontal)
        {
            return SendScriptAsync("editor.revealPosition(JSON.parse('" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + "'), " + JsonSerializer.Serialize(revealVerticalInCenter) + ", " + JsonSerializer.Serialize(revealHorizontal) + ")").AsAsyncAction();
        }

        /// <summary>Scrolls the editor to reveal the specified position in the center of the viewport.</summary>
        /// <param name="position">The position to reveal.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealPositionInCenterAsync(IPosition position)
        {
            return SendScriptAsync("editor.revealPositionInCenter(JSON.parse('" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + "'))").AsAsyncAction();
        }

        /// <summary>Scrolls the editor to reveal the specified position in the center, only if outside the viewport.</summary>
        /// <param name="position">The position to reveal.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealPositionInCenterIfOutsideViewportAsync(IPosition position)
        {
            return SendScriptAsync("editor.revealPositionInCenterIfOutsideViewport(JSON.parse('" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + "'))").AsAsyncAction();
        }

        /// <summary>Scrolls the editor to reveal the specified range.</summary>
        /// <param name="range">The range to reveal.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealRangeAsync(IRange range)
        {
            return SendScriptAsync("editor.revealRange(JSON.parse('" + JsonSerializer.Serialize(Range.Lift(range), MonacoJsonContext.Relaxed.Range) + "'))").AsAsyncAction();
        }

        /// <summary>Scrolls the editor to reveal the specified range at the top of the viewport.</summary>
        /// <param name="range">The range to reveal.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealRangeAtTopAsync(IRange range)
        {
            return SendScriptAsync("editor.revealRangeAtTop(JSON.parse('" + JsonSerializer.Serialize(Range.Lift(range), MonacoJsonContext.Relaxed.Range) + "'))").AsAsyncAction();
        }

        /// <summary>Scrolls the editor to reveal the specified range in the center of the viewport.</summary>
        /// <param name="range">The range to reveal.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealRangeInCenterAsync(IRange range)
        {
            return SendScriptAsync("editor.revealRangeInCenter(JSON.parse('" + JsonSerializer.Serialize(Range.Lift(range), MonacoJsonContext.Relaxed.Range) + "'))").AsAsyncAction();
        }

        /// <summary>Scrolls the editor to reveal the specified range in the center, only if outside the viewport.</summary>
        /// <param name="range">The range to reveal.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        public IAsyncAction RevealRangeInCenterIfOutsideViewportAsync(IRange range)
        {
            return SendScriptAsync("editor.revealRangeInCenterIfOutsideViewport(JSON.parse('" + JsonSerializer.Serialize(Range.Lift(range), MonacoJsonContext.Relaxed.Range) + "'))").AsAsyncAction();
        }
        #endregion

        /// <summary>
        /// Registers a custom action in the editor that appears in the context menu and command palette.
        /// Works on both WASM and desktop platforms.
        /// </summary>
        /// <param name="action">The action descriptor defining the label, keybindings, and run callback.</param>
        /// <returns>An asynchronous action that completes when the action is registered in Monaco.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the editor is not yet initialized. Call this method after <see cref="EditorLoaded"/> fires.
        /// </exception>
        /// <remarks>Wraps Monaco <c>editor.addAction</c>.</remarks>
        public IAsyncAction AddActionAsync(IActionDescriptor action)
        {
            if (_parentAccessor is null)
            {
                throw new InvalidOperationException(
                    "The editor bridge is not initialized. Call AddActionAsync after EditorLoaded fires.");
            }

            var wref = new WeakReference<CodeEditor>(this);
            _parentAccessor.RegisterAction("Action" + action.Id, new Action(() => { if (wref.TryGetTarget(out var editor)) { action?.Run(editor, null); } }));
            return InvokeScriptAsync("addAction", action).AsAsyncAction();
        }

        /// <summary>
        /// Evaluates a raw JavaScript expression in the editor's web view and returns the
        /// result as a string.
        /// </summary>
        /// <param name="script">The JavaScript expression to evaluate.</param>
        /// <returns>The evaluation result as a string, or <see langword="null"/> if the
        /// presenter is not available.</returns>
        public async Task<string?> InvokeScriptAsync(string script)
        {
            if (_view is not null)
            {
                var r = await _view.RunScriptAsync<object>(script);
                return r?.ToString();
            }

            return null;
        }

        private int _commandIndex = 0;

        /// <summary>
        /// Registers a command with no keybinding in the editor.
        /// Works on both WASM and desktop platforms.
        /// </summary>
        /// <param name="handler">The callback to invoke when the command is triggered.</param>
        /// <returns>The command identifier string, or <see langword="null"/> on failure.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the editor is not yet initialized. Call this method after <see cref="EditorLoaded"/> fires.
        /// </exception>
        public async Task<string?> AddCommandAsync(CommandHandler handler)
        {
            return await AddCommandAsync(0, handler, string.Empty);
        }

        /// <summary>
        /// Registers a keybinding-triggered command in the editor.
        /// Works on both WASM and desktop platforms.
        /// </summary>
        /// <param name="keybinding">The Monaco keybinding code. Use <c>0</c> for no binding.</param>
        /// <param name="handler">The callback to invoke when the command is triggered.</param>
        /// <returns>The command identifier string, or <see langword="null"/> on failure.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the editor is not yet initialized. Call this method after <see cref="EditorLoaded"/> fires.
        /// </exception>
        public async Task<string?> AddCommandAsync(int keybinding, CommandHandler handler)
        {
            return await AddCommandAsync(keybinding, handler, string.Empty);
        }

        /// <summary>
        /// Registers a keybinding-triggered command with an optional context key expression.
        /// Works on both WASM and desktop platforms.
        /// </summary>
        /// <param name="keybinding">The Monaco keybinding code. Use <c>0</c> for no binding.</param>
        /// <param name="handler">The callback to invoke when the command is triggered.</param>
        /// <param name="context">A Monaco context key expression that gates when the command is active.</param>
        /// <returns>The command identifier string, or <see langword="null"/> on failure.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the editor is not yet initialized. Call this method after <see cref="EditorLoaded"/> fires.
        /// </exception>
        /// <remarks>
        /// Wraps Monaco <c>editor.addCommand</c>. Command parameters arrive as
        /// <see cref="System.Text.Json.JsonElement"/> instances (breaking change from the
        /// prior Newtonsoft <c>JObject</c> return type).
        /// </remarks>
        public async Task<string?> AddCommandAsync(int keybinding, CommandHandler handler, string context)
        {
            if (_parentAccessor is null)
            {
                throw new InvalidOperationException(
                    "The editor bridge is not initialized. Call AddCommandAsync after EditorLoaded fires.");
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

        /// <summary>
        /// Creates a boolean context key that can gate command and action availability.
        /// </summary>
        /// <param name="key">The context key name.</param>
        /// <param name="defaultValue">The initial value of the context key.</param>
        /// <returns>The created <see cref="ContextKey"/> instance.</returns>
        /// <remarks>Wraps Monaco <c>editor.createContextKey</c>.</remarks>
        public async Task<ContextKey> CreateContextKeyAsync(string key, bool defaultValue)
        {
            var ck = new ContextKey(this, key, defaultValue);

            await InvokeScriptAsync("createContext", ck);

            return ck;
        }

        /// <summary>
        /// Gets the current text model associated with the editor.
        /// </summary>
        /// <returns>The current <see cref="IModel"/>, or <see langword="null"/> if the model
        /// has not been initialized.</returns>
        public IModel? GetModel()
        {
            return _model;
        }

        /// <summary>
        /// Retrieves all diagnostic markers currently set on the editor's model.
        /// </summary>
        /// <returns>A collection of <see cref="Marker"/> instances.</returns>
        /// <remarks>Wraps Monaco <c>editor.getModelMarkers</c>.</remarks>
        public async Task<IEnumerable<Marker?>> GetModelMarkersAsync() // TODO: Filter (string? owner, Uri? resource, int? take)
        {
            return await SendScriptAsync<IEnumerable<Marker>>("monaco.editor.getModelMarkers();").AsAsyncOperation();
        }

        /// <summary>
        /// Sets diagnostic markers on the editor's model for the specified owner.
        /// </summary>
        /// <param name="owner">An owner identifier used to group markers for later clearing.</param>
        /// <param name="markers">The marker data to apply.</param>
        /// <remarks>
        /// Wraps Monaco <c>editor.setModelMarkers</c>. Use the <see cref="Markers"/> property
        /// for data-bound marker management; do not mix both approaches.
        /// </remarks>
        public async Task SetModelMarkersAsync(string owner, IMarkerData[] markers)
        {
            await SendScriptAsync("monaco.editor.setModelMarkers(EditorContext.getEditorForElement(element).model, " + JsonSerializer.Serialize(owner, MonacoJsonContext.Relaxed.Options) + ", " + JsonSerializer.Serialize(markers, MonacoJsonContext.Relaxed.Options) + ");").AsAsyncAction();
        }

        /// <summary>
        /// Gets the current cursor position in the editor.
        /// </summary>
        /// <returns>The current <see cref="Position"/>, or <see langword="null"/> if the
        /// editor is not initialized.</returns>
        /// <remarks>Wraps Monaco <c>editor.getPosition</c>.</remarks>
        public async Task<Position?> GetPositionAsync()
        {
            return await SendScriptAsync<Position>("EditorContext.getEditorForElement(element).editor.getPosition();").AsAsyncOperation();
        }

        /// <summary>
        /// Sets the cursor position in the editor.
        /// </summary>
        /// <param name="position">The target position.</param>
        /// <returns>An asynchronous action that completes when the script executes.</returns>
        /// <remarks>Wraps Monaco <c>editor.setPosition</c>.</remarks>
        public IAsyncAction SetPositionAsync(IPosition position)
        {
            return SendScriptAsync("EditorContext.getEditorForElement(element).editor.setPosition(" + JsonSerializer.Serialize(Position.Lift(position), MonacoJsonContext.Relaxed.Position) + ");").AsAsyncAction();
        }

        /// <summary>
        /// Replaces all editor decorations with the specified set, updating CSS styles as needed.
        /// </summary>
        /// <param name="newDecorations">The replacement decoration set.</param>
        /// <remarks>
        /// Wraps Monaco <see href="https://microsoft.github.io/monaco-editor/typedoc/interfaces/editor_editor_api.editor.ICodeEditor.html#deltaDecorations">editor.deltaDecorations</see>.
        /// Prefer the <see cref="Decorations"/> property for data-bound management.
        /// </remarks>
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
