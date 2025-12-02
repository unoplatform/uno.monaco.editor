using Monaco.Extensions;

namespace Monaco
{

    public sealed partial class LanguagesHelper
    {
#pragma warning disable CA1822 // Mark members as static
        public string GetCodeLanguageFromExtension(string extension) => NativeMethods.LanguageIdFromExtension(extension);
#pragma warning restore CA1822 // Mark members as static
    }
}
