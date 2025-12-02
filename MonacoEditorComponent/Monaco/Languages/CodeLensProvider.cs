using Monaco.Editor;

namespace Monaco.Languages
{
    public interface CodeLensProvider
    {
        Task<CodeLensList> ProvideCodeLensesAsync(IModel model);

        Task<CodeLens> ResolveCodeLensAsync(IModel model, CodeLens codeLens);
    }
}

