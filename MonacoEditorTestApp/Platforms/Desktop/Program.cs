using Uno.UI.Hosting;

namespace MonacoEditorTestApp;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Matches the WebAssembly head, which has always called this. Without it the desktop
        // app produced no Uno logging at all -- including whatever the X11 host reports when
        // the native web view fails to initialize.
        App.InitializeLogging();

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
