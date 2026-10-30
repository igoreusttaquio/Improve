using Avalonia;
using Avalonia.Controls;
using Improve.App.Fonts;
using Improve.App.Services;
using Improve.Core.Storage;

namespace Improve.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Instância única: um segundo processo (ex.: "improve --trigger") repassa os argumentos e sai.
        var configDirectory = AppPaths.ConfigDirectory();
        using var instance = SingleInstance.TryAcquire(configDirectory);
        if (instance is null)
        {
            SingleInstance.SendToRunning(configDirectory, args);
            return;
        }

        // A configuração é carregada antes da UI; ainda não há contexto de sincronização aqui.
        using var services = AppServices.BuildAsync().GetAwaiter().GetResult();
        AppBuilder.Configure(() => new App(services, instance, args))
            .UsePlatformDetect()
            .ConfigureFonts(fonts => fonts.AddFontCollection(new ImproveFontCollection()))
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }
}
