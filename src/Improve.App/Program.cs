using Avalonia;
using Avalonia.Controls;
using Improve.App.Fonts;
using Improve.App.Services;
using Improve.Core.Storage;
using Improve.Platform.Windows;

namespace Improve.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Processo auxiliar elevado do modo administrador: só cria ou apaga a tarefa agendada.
        if (OperatingSystem.IsWindows() && WindowsElevation.RunHelper(args) is { } exitCode)
            return exitCode;

        var configDirectory = AppPaths.ConfigDirectory();
        if (ElevatedHandoff.TryHandOff(configDirectory, args))
            return 0;

        // Instância única: um segundo processo (ex.: "improve --trigger") repassa os argumentos e sai.
        using var instance = SingleInstance.TryAcquire(configDirectory, waitForPrevious: args.Contains("--elevated"));
        if (instance is null)
        {
            try
            {
                SingleInstance.SendToRunning(configDirectory, args);
            }
            catch (Exception e) when (e is TimeoutException or UnauthorizedAccessException or IOException)
            {
                // A instância aberta é elevada e não aceita o pipe de um processo comum.
                ElevatedHandoff.Write(configDirectory, args);
            }
            return 0;
        }

        // A configuração é carregada antes da UI; ainda não há contexto de sincronização aqui.
        using var services = AppServices.BuildAsync().GetAwaiter().GetResult();
        AppBuilder.Configure(() => new App(services, instance, args))
            .UsePlatformDetect()
            .ConfigureFonts(fonts => fonts.AddFontCollection(new ImproveFontCollection()))
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        return 0;
    }
}
