using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Monaco;

/// <summary>
/// One file's two sides in a <see cref="MultiDiffCodeEditor"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b><see langword="null"/> is not the same as an empty string.</b> A <see langword="null"/>
/// <see cref="OriginalText"/> or <see cref="ModifiedText"/> means that side of the comparison does
/// not exist, and the file renders with an <c>A</c> (added) or <c>D</c> (deleted) badge. An empty
/// string means a real, empty file, which renders as an ordinary diff against nothing. Getting
/// these two confused is the most common way to make a file list look wrong.
/// </para>
/// <para>
/// <see cref="Path"/> is the identity: it keys the underlying Monaco models, so it must be unique
/// within a control and stable across updates -- that is what preserves a file's scroll offset and
/// collapsed state when the list is re-pushed. Setting <see cref="OriginalPath"/> to something
/// different from <see cref="Path"/> renders the file as a rename (<c>R</c>), with the old name
/// struck through beside the new one.
/// </para>
/// </remarks>
public sealed class DiffFileEntry : INotifyPropertyChanged
{
    private string _path = string.Empty;
    private string? _originalPath;
    private string? _originalText;
    private string? _modifiedText;
    private string? _language;
    private bool _collapsed;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets the file's path. Doubles as identity and as the header label.
    /// </summary>
    /// <remarks>
    /// Must be unique within a <see cref="MultiDiffCodeEditor"/>; duplicates are skipped. Keeping
    /// it stable across updates is what preserves the file's scroll position and collapsed state.
    /// </remarks>
    public string Path
    {
        get => _path;
        set => Set(ref _path, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the path the file had on the original side. When set and different from
    /// <see cref="Path"/>, the file renders as a rename.
    /// </summary>
    public string? OriginalPath
    {
        get => _originalPath;
        set => Set(ref _originalPath, value);
    }

    /// <summary>
    /// Gets or sets the original ("before") contents.
    /// <see langword="null"/> omits the original side entirely and marks the file as added;
    /// <see cref="string.Empty"/> is a real but empty file.
    /// </summary>
    public string? OriginalText
    {
        get => _originalText;
        set => Set(ref _originalText, value);
    }

    /// <summary>
    /// Gets or sets the modified ("after") contents.
    /// <see langword="null"/> omits the modified side entirely and marks the file as deleted;
    /// <see cref="string.Empty"/> is a real but empty file.
    /// </summary>
    public string? ModifiedText
    {
        get => _modifiedText;
        set => Set(ref _modifiedText, value);
    }

    /// <summary>
    /// Gets or sets the syntax language for both sides. When <see langword="null"/>, it is
    /// inferred from the extension of <see cref="Path"/>.
    /// </summary>
    public string? Language
    {
        get => _language;
        set => Set(ref _language, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the file's section is collapsed.
    /// </summary>
    /// <remarks>
    /// Two-way: setting it collapses or expands the section, and a user clicking the chevron
    /// writes back here. A re-push of the file list only re-applies this when the value actually
    /// changed, so an unrelated text update does not undo the user's toggle.
    /// </remarks>
    public bool Collapsed
    {
        get => _collapsed;
        set => Set(ref _collapsed, value);
    }

    /// <summary>
    /// Whether both sides carry the same text -- a file listed with no changes.
    /// </summary>
    [JsonIgnore]
    public bool IsUnchanged => OriginalText == ModifiedText;

    private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
