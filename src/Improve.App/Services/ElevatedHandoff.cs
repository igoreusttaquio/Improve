using Improve.Core.Storage;

namespace Improve.App.Services;

/// <summary>
/// Repasse de argumentos para a instância elevada. Um processo comum não abre o mutex nem o pipe criados por um
/// processo elevado (o Windows bloqueia escrever "para cima"), então o pedido vai por um arquivo na pasta de
/// configuração, que a instância elevada vigia. Só os argumentos conhecidos são aceitos.
/// </summary>
public static class ElevatedHandoff
{
    private const string FileName = "handoff.args";
    private static readonly string[] Known = ["--trigger", App.BackgroundArgument];
    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);

    public static void Write(string configDirectory, string[] args)
    {
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(Path.Combine(configDirectory, FileName), string.Join('\n', args.Where(Known.Contains)));
    }

    public static void Discard(string configDirectory) => File.Delete(Path.Combine(configDirectory, FileName));

    /// <summary>Pedido deixado antes de a instância elevada abrir (pedidos velhos são descartados).</summary>
    public static string[]? TakePending(string configDirectory)
    {
        var path = Path.Combine(configDirectory, FileName);
        try
        {
            if (!File.Exists(path))
                return null;
            var fresh = DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < MaxAge;
            var args = File.ReadAllText(path).Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(Known.Contains).ToArray();
            File.Delete(path);
            return fresh ? args : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Avisa a cada pedido novo, fora da thread de UI. O retorno precisa ficar vivo enquanto o app roda.</summary>
    public static FileSystemWatcher Watch(string configDirectory, Action<string[]> onArguments)
    {
        var watcher = new FileSystemWatcher(configDirectory, FileName) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
        void OnChange(object? sender, FileSystemEventArgs e)
        {
            // O evento pode chegar antes de o outro processo terminar de gravar.
            Thread.Sleep(50);
            if (TakePending(configDirectory) is { } args)
                onArguments(args);
        }
        watcher.Created += OnChange;
        watcher.Changed += OnChange;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    /// <summary>
    /// No modo administrador, um processo comum passa a vez para a instância elevada e sai (<c>true</c>).
    /// <c>false</c>: segue como sempre (modo desligado, já elevado, ou a tarefa não existe mais).
    /// </summary>
    public static bool TryHandOff(string configDirectory, string[] args)
    {
        if (!OperatingSystem.IsWindows())
            return false;
        var elevation = new Improve.Platform.Windows.WindowsElevation();
        if (elevation.IsElevated || !RunAsAdminEnabled(configDirectory))
            return false;

        switch (SingleInstance.Probe(configDirectory))
        {
            case InstanceState.RunningAccessible:
                return false; // uma instância comum já roda: o caminho normal repassa pelo pipe
            case InstanceState.RunningElevated:
                Write(configDirectory, args);
                return true;
            default:
                Write(configDirectory, args);
                if (elevation.TryStartElevated())
                    return true;
                Discard(configDirectory);
                return false;
        }
    }

    private static bool RunAsAdminEnabled(string configDirectory)
    {
        try
        {
            return new SettingsStore(configDirectory).LoadSettingsAsync().GetAwaiter().GetResult().RunAsAdmin;
        }
        catch (SettingsException)
        {
            return false;
        }
    }
}
