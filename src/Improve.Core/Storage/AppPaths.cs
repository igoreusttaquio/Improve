namespace Improve.Core.Storage;

public static class AppPaths
{
    /// <summary>
    /// Pasta de configuração: Windows <c>%APPDATA%\improve</c>, macOS <c>~/Library/Application Support/improve</c>,
    /// Linux <c>$XDG_CONFIG_HOME/improve</c> (padrão <c>~/.config/improve</c>). <c>IMPROVE_CONFIG_DIR</c> substitui tudo.
    /// </summary>
    public static string ConfigDirectory()
    {
        // Permite usar outra pasta (testes manuais, instalação portátil).
        if (Environment.GetEnvironmentVariable("IMPROVE_CONFIG_DIR") is { Length: > 0 } custom)
            return custom;
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "improve");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            return Path.Combine(home, "Library", "Application Support", "improve");

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".config") : xdg, "improve");
    }
}
