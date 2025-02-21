using Monaco.Editor;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Monaco.Languages
{
    public interface CodeLensProvider
    {
        Task<CodeLensList> ProvideCodeLensesAsync(IModel model);

        Task<CodeLens> ResolveCodeLensAsync(IModel model, CodeLens codeLens);
    }
}

