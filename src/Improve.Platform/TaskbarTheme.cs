using Microsoft.Win32;

namespace Improve.Platform;

/// <summary>Tema da barra do sistema onde fica o ícone da bandeja.</summary>
public static class TaskbarTheme
{
    /// <summary>
    /// <c>true</c> se a barra for clara, <c>false</c> se for escura, <c>null</c> se o SO não informar.
    /// No Windows a barra tem tema próprio, independente do tema dos apps.
    /// </summary>
    public static bool? IsLight()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value ? value != 0 : null;
    }
}
