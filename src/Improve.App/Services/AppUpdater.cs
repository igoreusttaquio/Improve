using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Improve.Core.Updates;

namespace Improve.App.Services;

/// <summary>Atualização pelo GitHub: escolhe o instalador da plataforma, executa e fecha o app para liberar os arquivos.</summary>
public sealed class AppUpdater(HttpClient http)
{
    private readonly UpdateChecker _checker = new(http);

    public static Version CurrentVersion { get; } = UpdateChecker.TryParseVersion(
        typeof(AppUpdater).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0",
        out var version) ? version : new Version(0, 0, 0);

    /// <summary>Nome do arquivo na release (ver <c>release.yml</c>); <c>null</c> se não houver instalador para esta plataforma.</summary>
    public static string? AssetName =>
        OperatingSystem.IsWindows() && RuntimeInformation.OSArchitecture == Architecture.X64 ? "improve-setup-x64.exe"
        : OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == Architecture.X64 ? "improve-linux-amd64.deb"
        : OperatingSystem.IsMacOS() ? (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "improve-macos-arm64.dmg" : "improve-macos-x64.dmg")
        : null;

    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct)
    {
        if (AssetName is not { } asset)
            return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        return await _checker.CheckAsync(CurrentVersion, asset, timeout.Token).ConfigureAwait(false);
    }

    public Task<string> DownloadAsync(UpdateInfo update, IProgress<double> progress, CancellationToken ct) =>
        _checker.DownloadAsync(update, Path.Combine(Path.GetTempPath(), "improve-update"), progress, ct);

    /// <summary>Abre o instalador e encerra o improve, para o instalador não encontrar os arquivos em uso.</summary>
    public static void InstallAndExit(string installer)
    {
        if (OperatingSystem.IsWindows())
        {
            // Inno Setup silencioso; /RELAUNCH=1 abre o improve de novo ao terminar (ver improve.iss).
            Process.Start(new ProcessStartInfo(installer, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RELAUNCH=1") { UseShellExecute = true });
        }
        else
        {
            // .deb pede senha de administrador e .dmg pede arrastar para Aplicativos: o sistema conduz a instalação.
            Process.Start(new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open", $"\"{installer}\"") { UseShellExecute = false });
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
