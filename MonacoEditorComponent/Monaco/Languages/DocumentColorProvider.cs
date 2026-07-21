using Monaco.Editor;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Monaco.Languages
{
    /// <summary>
    /// A provider of colors for editor models.
    /// <seealso href="https://microsoft.github.io/monaco-editor/typedoc/interfaces/editor_editor_api.languages.DocumentColorProvider.html">monaco.languages.DocumentColorProvider</seealso>
    /// </summary>
    public interface DocumentColorProvider
    {
        /// <summary>
        /// Provide the string representations for a color.
        /// </summary>
        Task<IEnumerable<ColorPresentation>> ProvideColorPresentationsAsync(IModel model, ColorInformation colorInfo);

        /// <summary>
        /// Provides the color ranges for a specific model.
        /// </summary>
        Task<IEnumerable<ColorInformation>> ProvideDocumentColorsAsync(IModel model);
    }
}
