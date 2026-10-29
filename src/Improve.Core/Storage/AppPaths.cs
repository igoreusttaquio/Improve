namespace Improve.Core.Storage;

public static class AppPaths
{
    /// <summary>
    /// Pasta de configuração: Windows <c>%APPDATA%\improve</c>, macOS <c>~/Library/Application Support/improve</c>,
    /// Linux <c>$XDG_CONFIG_HOME/improve</c> (padrão <c>~/.config/improve</c>).
    /// </summary>
    public static string ConfigDirectory()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "improve");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            return Path.Combine(home, "Library", "Application Support", "improve");

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".config") : xdg, "improve");
    }
}
