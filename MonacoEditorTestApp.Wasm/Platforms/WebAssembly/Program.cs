namespace MonacoEditorTestApp;

public class Program
{
    private static App? _app;

    public static async Task<int> Main(string[] args)
    {
        App.InitializeLogging();

        var host = new global::Uno.UI.Runtime.Skia.WebAssembly.Browser.PlatformHost(() => _app = new App());
        await host.Run();

        return 0;
    }
}
