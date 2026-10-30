using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace Improve.Platform.Windows;

/// <summary>
/// Modo administrador por uma tarefa agendada com privilégio máximo: é o jeito de o Windows abrir um app elevado
/// sem perguntar a cada vez (a chave Run não eleva). Criar ou apagar a tarefa exige estar elevado; quando não está,
/// o improve se chama de novo com "runas" só para isso (<see cref="RunHelper"/>), e o Windows pede confirmação uma vez.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsElevation : IElevationService
{
    public const string TaskName = "improve-admin";

    /// <summary>Argumento da instância aberta pela tarefa: espera a instância comum fechar antes de assumir.</summary>
    public const string ElevatedArgument = "--elevated";

    private const string HelperArgument = "--admin-task";

    public bool IsElevated { get; } = CheckElevated();

    public bool TryStartElevated() => Schtasks($"/Run /TN \"{TaskName}\"") == 0;

    public Task<bool> RegisterAsync(bool startWithSystem) =>
        IsElevated ? Task.FromResult(Register(startWithSystem)) : RunHelperElevatedAsync($"register {(startWithSystem ? 1 : 0)}");

    public Task<bool> UnregisterAsync() =>
        IsElevated ? Task.FromResult(Unregister()) : RunHelperElevatedAsync("unregister");

    /// <summary>Executa o pedido do processo auxiliar elevado; <c>null</c> se os argumentos não são dele.</summary>
    public static int? RunHelper(string[] args)
    {
        if (args is not [HelperArgument, var action, ..])
            return null;
        var ok = action switch
        {
            "register" => Register(startWithSystem: args is [_, _, "1", ..]),
            "unregister" => Unregister(),
            _ => false,
        };
        return ok ? 0 : 1;
    }

    private static async Task<bool> RunHelperElevatedAsync(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"{HelperArgument} {arguments}")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            if (process is null)
                return false;
            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            // Inclui o usuário recusar a confirmação do Windows (ERROR_CANCELLED).
            return false;
        }
    }

    private static bool Register(bool startWithSystem)
    {
        var file = Path.Combine(Path.GetTempPath(), $"improve-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(file, TaskXml(startWithSystem), Encoding.Unicode);
            return Schtasks($"/Create /TN \"{TaskName}\" /XML \"{file}\" /F") == 0;
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static bool Unregister() =>
        Schtasks($"/Query /TN \"{TaskName}\"") != 0 || Schtasks($"/Delete /TN \"{TaskName}\" /F") == 0;

    // Sem gatilho, a tarefa só roda sob demanda (ao abrir o improve); com "iniciar com o sistema", também no logon.
    private static string TaskXml(bool startWithSystem)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = SecurityElement.Escape(identity.User!.Value);
        var trigger = startWithSystem ? $"<LogonTrigger><UserId>{user}</UserId></LogonTrigger>" : "";
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>improve (administrador)</Description></RegistrationInfo>
              <Triggers>{trigger}</Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>4</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{SecurityElement.Escape(Environment.ProcessPath)}</Command>
                  <Arguments>--background {ElevatedArgument}</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static int Schtasks(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null)
                return -1;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception)
        {
            return -1;
        }
    }

    private static bool CheckElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
