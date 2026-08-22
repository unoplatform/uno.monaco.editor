using Microsoft.UI.Dispatching;

namespace Monaco
{
    /// <summary>
    /// Provides a cross-platform Uno Platform wrapper around the
    /// <see href="https://microsoft.github.io/monaco-editor/">Monaco Editor</see>.
    /// On WebAssembly the editor runs natively in the browser; on desktop (Skia) it is
    /// hosted inside a WebView2 control with a JSON-RPC bridge for interop.
    /// </summary>
    /// <remarks>
    /// Hosts a single editable document exposed through <see cref="Text"/>. For a
    /// two-sided comparison view, use <c>DiffCodeEditor</c> instead. All host, lifecycle,
    /// and bridge behavior lives on <see cref="CodeEditorBase"/>.
    /// </remarks>
    public sealed partial class CodeEditor : CodeEditorBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CodeEditor"/> class on the current UI thread.
        /// </summary>
        public CodeEditor() : this(null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeEditor"/> class with an explicit dispatcher.
        /// </summary>
        /// <param name="queue">
        /// The <see cref="DispatcherQueue"/> for the UI thread. When <see langword="null"/>, the
        /// current thread's dispatcher is used.
        /// </param>
        public CodeEditor(DispatcherQueue? queue) : base(queue)
        {
            DefaultStyleKey = typeof(CodeEditor);
        }

        /// <inheritdoc />
        protected override string? PrimaryText => Text;
    }
}
