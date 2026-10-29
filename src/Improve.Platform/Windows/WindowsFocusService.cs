using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Improve.Core.Execution;
using static Improve.Platform.Windows.NativeMethods;

namespace Improve.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsFocusService : IFocusService
{
    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;

    public FocusTarget? Capture()
    {
        var window = GetForegroundWindow();
        if (window == 0)
            return null;
        GetWindowThreadProcessId(window, out var processId);
        return processId == OwnProcessId ? null : new FocusTarget(window, (int)processId, AppName((int)processId));
    }

    public async Task<bool> RestoreAsync(FocusTarget target, CancellationToken ct)
    {
        if (!IsWindow(target.Handle))
            return false;
        var current = GetForegroundWindow();
        if (current == target.Handle)
            return true;
        // Outra janela que não é do improve está em foco: o usuário mudou de janela.
        GetWindowThreadProcessId(current, out var currentProcess);
        if (current != 0 && currentProcess != OwnProcessId)
            return false;

        ForceForeground(target.Handle);
        for (var i = 0; i < 20; i++)
        {
            if (GetForegroundWindow() == target.Handle)
                return true;
            await Task.Delay(10, ct).ConfigureAwait(false);
        }
        return false;
    }

    /// <summary>Se o foco está numa janela do próprio improve (para ignorar os atalhos nela).</summary>
    public static bool IsOwnWindowFocused()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var processId);
        return processId == OwnProcessId;
    }

    // O Windows só deixa trocar o foco quem recebeu a última entrada; ligar a fila de entrada à da
    // janela em foco contorna essa trava.
    private static void ForceForeground(nint window)
    {
        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var ownThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != ownThread && AttachThreadInput(ownThread, foregroundThread, true);
        try
        {
            if (IsIconic(window))
                ShowWindow(window, SW_RESTORE);
            SetForegroundWindow(window);
            BringWindowToTop(window);
        }
        finally
        {
            if (attached)
                AttachThreadInput(ownThread, foregroundThread, false);
        }
    }

    // Nome amigável ("Bloco de Notas") quando o executável informa; senão, o nome do processo.
    private static string? AppName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            try
            {
                var description = process.MainModule?.FileVersionInfo.FileDescription;
                if (!string.IsNullOrWhiteSpace(description))
                    return description;
            }
            catch (Win32Exception)
            {
                // Processo elevado ou protegido: sem acesso ao módulo.
            }
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
