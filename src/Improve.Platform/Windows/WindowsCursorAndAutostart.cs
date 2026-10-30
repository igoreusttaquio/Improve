using System.Runtime.Versioning;
using Microsoft.Win32;
using static Improve.Platform.Windows.NativeMethods;

namespace Improve.Platform.Windows;

/// <summary>Cursor em pixels físicos (o app é DPI-aware por monitor, então não há escala virtual).</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCursorService : ICursorService
{
    public (int X, int Y) GetPosition() => GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);
}

[SupportedOSPlatform("windows")]
public sealed class WindowsWindowActivator : IWindowActivator
{
    public void Activate(nint windowHandle) => WindowsFocusService.ForceForeground(windowHandle);
}

/// <summary>Iniciar com o Windows pela chave Run do usuário (HKCU), sem precisar de administrador.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsAutostart : IAutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "improve";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) as string == Command;
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, Command);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
