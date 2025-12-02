using Microsoft.UI.Text;
using Uno.UI.Hosting;
using Uno.UI.Xaml.Media;

namespace MonacoEditorTestApp;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        App.InitializeLogging();

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWebAssembly()
            .Build();

        await host.RunAsync();

        return 0;
    }
}
