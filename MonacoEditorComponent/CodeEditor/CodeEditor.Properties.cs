using Microsoft.UI.Xaml;

namespace Monaco;

partial class CodeEditor
{
    /* Text: The editable document hosted by this control.
     */

    #region DependencyProperty: Text

    /// <summary>Identifies the <see cref="Text"/> dependency property.</summary>
    public static DependencyProperty TextProperty { get; } = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(CodeEditor),
        new PropertyMetadata(string.Empty, OnTextChanged));

    /// <summary>
    /// Gets or sets the text content of the editor.
    /// </summary>
    /// <remarks>
    /// Setting this property after the editor is loaded invokes the Monaco
    /// <c>editor.setValue</c> API via <c>updateContent</c>. Changes originating
    /// from JavaScript are pushed back through the bridge and suppress re-entrant
    /// notifications via <see cref="EditorHostBase.IsSettingValue"/>.
    /// </remarks>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    #endregion

    private static void OnTextChanged(DependencyObject control, DependencyPropertyChangedEventArgs e) => ((CodeEditor)control).OnTextChanged(e);
}
