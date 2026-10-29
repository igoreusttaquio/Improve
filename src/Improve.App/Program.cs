using Avalonia;
using Avalonia.Controls;
using Improve.App.Fonts;
using Improve.App.Services;

namespace Improve.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // A configuração é carregada antes da UI; ainda não há contexto de sincronização aqui.
        using var services = AppServices.BuildAsync().GetAwaiter().GetResult();
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .ConfigureFonts(fonts => fonts.AddFontCollection(new ImproveFontCollection()))
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }
}
