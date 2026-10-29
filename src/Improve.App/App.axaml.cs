using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Improve.App.Resources;
using Improve.App.ViewModels;
using Improve.App.Views;
using Improve.Core.Models;
using Improve.Core.Storage;
using Improve.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Improve.App;

public partial class App(IServiceProvider services) : Application
{
    private SettingsWindow? _settings;

    /// <summary>Com o improve pausado, o atalho global não abre o popup (fase 3).</summary>
    public bool IsPaused { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var config = services.GetRequiredService<ConfigState>();
        ApplyTheme(config.Settings.Theme);
        config.Changed += (_, _) => Dispatcher.UIThread.Post(() => ApplyTheme(config.Settings.Theme));

        UpdateTrayGlyph();
        if (PlatformSettings is { } settings)
            settings.ColorValuesChanged += (_, _) => UpdateTrayGlyph();
        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyTheme(ThemePreference theme) => RequestedThemeVariant = theme switch
    {
        ThemePreference.Dark => ThemeVariant.Dark,
        ThemePreference.Light => ThemeVariant.Light,
        _ => ThemeVariant.Default,
    };

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
            _settings = new SettingsWindow(services.GetRequiredService<SettingsViewModel>());
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show();
        _settings.Activate();
    }

    private void OnTogglePause(object? sender, EventArgs e)
    {
        IsPaused = !IsPaused;
        if (sender is NativeMenuItem item)
            item.Header = IsPaused ? Strings.TrayResume : Strings.TrayPause;
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
