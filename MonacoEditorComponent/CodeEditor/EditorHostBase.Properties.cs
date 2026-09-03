using Microsoft.UI.Xaml;

using Monaco.Editor;
using Monaco.Helpers;

using Windows.Foundation.Collections;

namespace Monaco;

public abstract partial class EditorHostBase : IParentAccessorAcceptor
{
    /* IsEditorLoaded: Read-only. True once Monaco has finished its initialization lifecycle.
     * RenderingBackend: Read-only. Wasm or Desktop, decided at construction.
     * SelectedText: The primary selection's text.
     * SelectedRange: The primary selection's range. Not pushed to Monaco; written by the bridge.
     * CodeLanguage: Syntax language id. Mirrors into Options.Language.
     * ReadOnly: Locks the document. Mirrors into Options.ReadOnly.
     * Options: The Monaco construction options. Replacing it may overwrite the pass-through
     *      properties above; per-property changes on the assigned instance are forwarded.
     * HasGlyphMargin: Shows the glyph margin. Mirrors into Options.GlyphMargin.
     * Decorations: Model decorations, applied as a delta whenever the vector changes.
     * Markers: Diagnostic markers, owner "CodeEditor".
     *
     * Everything from SelectedText down is the single-document surface and is inert on a control
     * whose HasPrimaryDocument is false -- see the remarks on that property.
     */

    internal static class DefaultValues
    {
        public const string CodeLanguage = "xml";
    }

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

    #region DependencyProperty: IsEditorLoaded

    /// <summary>Identifies the <see cref="IsEditorLoaded"/> dependency property.</summary>
    public static DependencyProperty IsEditorLoadedProperty { get; } = DependencyProperty.Register(
        nameof(IsEditorLoaded),
        typeof(bool),
        typeof(EditorHostBase),
        new PropertyMetadata(default(bool), OnIsEditorLoadedChanged));

    /// <summary>
    /// Gets a value indicating whether the Monaco editor has completed its initialization
    /// lifecycle and is ready to receive commands.
    /// </summary>
    /// <remarks>
    /// This property transitions to <see langword="true"/> after the editor fires
    /// <see cref="EditorHostBase.EditorLoaded"/>. It can be used in XAML templates to control
    /// visibility and prevent displaying an empty WebView during loading.
    /// </remarks>
    public bool IsEditorLoaded
    {
        get => (bool)GetValue(IsEditorLoadedProperty);
        private set => SetValue(IsEditorLoadedProperty, value);
    }

    #endregion
    #region DependencyProperty: RenderingBackend = Wasm on browser, Desktop otherwise

    /// <summary>Identifies the <see cref="RenderingBackend"/> dependency property.</summary>
    public static DependencyProperty RenderingBackendProperty { get; } = DependencyProperty.Register(
        nameof(RenderingBackend),
        typeof(RenderingBackend),
        typeof(EditorHostBase),
        new PropertyMetadata(OperatingSystem.IsBrowser() ? RenderingBackend.Wasm : RenderingBackend.Desktop));

    /// <summary>
    /// Gets the rendering backend used by the editor (Wasm or Desktop).
    /// </summary>
    public RenderingBackend RenderingBackend
    {
        get => (RenderingBackend)GetValue(RenderingBackendProperty);
        private set => SetValue(RenderingBackendProperty, value);
    }

    #endregion
    #region DependencyProperty: SelectedText

    /// <summary>Identifies the <see cref="SelectedText"/> dependency property.</summary>
    public static DependencyProperty SelectedTextProperty { get; } = DependencyProperty.Register(
        nameof(SelectedText),
        typeof(string),
        typeof(EditorHostBase),
        new PropertyMetadata(string.Empty, OnSelectedTextChanged));

    /// <summary>
    /// Gets or sets the currently selected text in the primary selection of the editor.
    /// </summary>
    public string SelectedText
    {
        get => (string)GetValue(SelectedTextProperty);
        set => SetValue(SelectedTextProperty, value);
    }

    #endregion
    #region DependencyProperty: SelectedRange

    /// <summary>Identifies the <see cref="SelectedRange"/> dependency property.</summary>
    public static DependencyProperty SelectedRangeProperty { get; } = DependencyProperty.Register(
        nameof(SelectedRange),
        typeof(Selection),
        typeof(EditorHostBase),
        new PropertyMetadata(default(Selection)));

    /// <summary>
    /// Gets or sets the current primary selection range in the editor.
    /// </summary>
    public Selection SelectedRange
    {
        get => (Selection)GetValue(SelectedRangeProperty);
        set => SetValue(SelectedRangeProperty, value);
    }

    #endregion
    #region DependencyProperty: CodeLanguage = "xml"

    /// <summary>Identifies the <see cref="CodeLanguage"/> dependency property.</summary>
    public static DependencyProperty CodeLanguageProperty { get; } = DependencyProperty.Register(
        nameof(CodeLanguage),
        typeof(string),
        typeof(EditorHostBase),
        new PropertyMetadata(DefaultValues.CodeLanguage, OnCodeLanguageChanged));

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

    #endregion
    #region DependencyProperty: ReadOnly

    /// <summary>Identifies the <see cref="ReadOnly"/> dependency property.</summary>
    public static DependencyProperty ReadOnlyProperty { get; } = DependencyProperty.Register(
        nameof(ReadOnly),
        typeof(bool),
        typeof(EditorHostBase),
        new PropertyMetadata(default(bool), OnReadOnlyChanged));

    /// <summary>
    /// Gets or sets a value indicating whether the editor is in read-only mode.
    /// </summary>
    public bool ReadOnly
    {
        get => (bool)GetValue(ReadOnlyProperty);
        set => SetValue(ReadOnlyProperty, value);
    }

    #endregion
    #region DependencyProperty: Options

    /// <summary>Identifies the <see cref="Options"/> dependency property.</summary>
    public static DependencyProperty OptionsProperty { get; } = DependencyProperty.Register(
        nameof(Options),
        typeof(StandaloneEditorConstructionOptions),
        typeof(EditorHostBase),
        new PropertyMetadata(default(StandaloneEditorConstructionOptions), OnOptionsChanged));

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

    #endregion
    #region DependencyProperty: HasGlyphMargin

    /// <summary>Identifies the <see cref="HasGlyphMargin"/> dependency property.</summary>
    public static DependencyProperty HasGlyphMarginProperty { get; } = DependencyProperty.Register(
        nameof(HasGlyphMargin),
        typeof(bool),
        typeof(EditorHostBase),
        new PropertyMetadata(default(bool), OnHasGlyphMarginChanged));

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

    #endregion
    #region DependencyProperty: Decorations

    /// <summary>Identifies the <see cref="Decorations"/> dependency property.</summary>
    public static DependencyProperty DecorationsProperty { get; } = DependencyProperty.Register(
        nameof(Decorations),
        typeof(IObservableVector<IModelDeltaDecoration>),
        typeof(EditorHostBase),
        new PropertyMetadata(default(IObservableVector<IModelDeltaDecoration>), OnDecorationsChanged));

    /// <summary>
    /// Gets or sets text Decorations.
    /// </summary>
    public IObservableVector<IModelDeltaDecoration> Decorations
    {
        get => (IObservableVector<IModelDeltaDecoration>)GetValue(DecorationsProperty);
        set => SetValue(DecorationsProperty, value);
    }

    #endregion
    #region DependencyProperty: Markers

    /// <summary>Identifies the <see cref="Markers"/> dependency property.</summary>
    public static DependencyProperty MarkersProperty { get; } = DependencyProperty.Register(
        nameof(Markers),
        typeof(IObservableVector<IMarkerData>),
        typeof(EditorHostBase),
        new PropertyMetadata(default(IObservableVector<IMarkerData>), OnMarkersChanged));

    /// <summary>
    /// Gets or sets the hint Markers.
    /// Note: This property is a helper for <see cref="SetModelMarkersAsync(string, IMarkerData[])"/>; use this property or the method, not both.
    /// </summary>
    public IObservableVector<IMarkerData> Markers
    {
        get => (IObservableVector<IMarkerData>)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    #endregion

    private static void OnIsEditorLoadedChanged(DependencyObject control, DependencyPropertyChangedEventArgs e) => ((EditorHostBase)control).OnIsEditorLoadedChanged(e);
    private static void OnSelectedTextChanged(DependencyObject control, DependencyPropertyChangedEventArgs e) => ((EditorHostBase)control).OnSelectedTextChanged(e);
    private static void OnCodeLanguageChanged(DependencyObject control, DependencyPropertyChangedEventArgs e) => ((EditorHostBase)control).OnCodeLanguageChanged(e);
    private static void OnReadOnlyChanged(DependencyObject control, DependencyPropertyChangedEventArgs e) => ((EditorHostBase)control).OnReadOnlyChanged(e);
    private static void OnOptionsChanged(DependencyObject control, DependencyPropertyChangedEventArgs e) => ((EditorHostBase)control).OnOptionsChanged(e);
    private static void OnHasGlyphMarginChanged(DependencyObject control, DependencyPropertyChangedEventArgs e) => ((EditorHostBase)control).OnHasGlyphMarginChanged(e);
    private static void OnDecorationsChanged(DependencyObject control, DependencyPropertyChangedEventArgs e) => ((EditorHostBase)control).OnDecorationsChanged(e);
    private static void OnMarkersChanged(DependencyObject control, DependencyPropertyChangedEventArgs e) => ((EditorHostBase)control).OnMarkersChanged(e);
}
