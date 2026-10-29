using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Improve.App.Views;
using Improve.Platform;

namespace Improve.App;

public partial class App : Application
{
    private SettingsWindow? _settings;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        UpdateTrayGlyph();
        if (PlatformSettings is { } settings)
            settings.ColorValuesChanged += (_, _) => UpdateTrayGlyph();
        base.OnFrameworkInitializationCompleted();
    }

    // Glifo claro em barra escura e vice-versa. No macOS o glifo "mono" é usado como template.
    private void UpdateTrayGlyph()
    {
        if (TrayIcon.GetIcons(this) is not [var tray, ..])
            return;
        var lightBar = OperatingSystem.IsMacOS()
            || (TaskbarTheme.IsLight() ?? ActualThemeVariant == ThemeVariant.Light);
        var variant = lightBar ? "mono" : "light";
        using var stream = AssetLoader.Open(new Uri($"avares://improve/Assets/Tray/glyph-{variant}-32.png"));
        tray.Icon = new WindowIcon(stream);
    }

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        if (_settings is null)
        {
            _settings = new SettingsWindow();
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show();
        _settings.Activate();
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
