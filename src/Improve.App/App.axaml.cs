using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Improve.App.Resources;
using Improve.App.Services;
using Improve.App.ViewModels;
using Improve.App.Views;
using Improve.Core.Models;
using Improve.Core.Storage;
using Improve.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Improve.App;

public partial class App(IServiceProvider services, SingleInstance instance) : Application
{
    private SettingsWindow? _settings;
    private FlowController? _flow;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var config = services.GetRequiredService<ConfigState>();
        var autostart = services.GetService<IAutostartService>();
        ApplySettings(config, autostart);
        config.Changed += (_, _) => Dispatcher.UIThread.Post(() => ApplySettings(config, autostart));

        // Integrações de SO (por enquanto só no Windows).
        _flow = services.GetService<FlowController>();
        _flow?.Start();
        instance.Listen(args => Dispatcher.UIThread.Post(() => OnSecondInstance(args)));

        UpdateTrayGlyph();
        if (PlatformSettings is { } settings)
            settings.ColorValuesChanged += (_, _) => UpdateTrayGlyph();
        base.OnFrameworkInitializationCompleted();
    }

    private void ApplySettings(ConfigState config, IAutostartService? autostart)
    {
        RequestedThemeVariant = config.Settings.Theme switch
        {
            ThemePreference.Dark => ThemeVariant.Dark,
            ThemePreference.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default,
        };
        if (autostart is not null && autostart.IsEnabled() != config.Settings.StartWithSystem)
            autostart.SetEnabled(config.Settings.StartWithSystem);
    }

    // "improve --trigger" executa o atalho global; abrir o app de novo mostra as configurações.
    private void OnSecondInstance(string[] args)
    {
        if (args.Contains("--trigger") && _flow is not null)
            _ = _flow.TriggerAsync();
        else
            OnOpenSettings(null, EventArgs.Empty);
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
            _settings = new SettingsWindow(services.GetRequiredService<SettingsViewModel>());
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show();
        _settings.Activate();
    }

    private void OnTogglePause(object? sender, EventArgs e)
    {
        if (_flow is null)
            return;
        _flow.IsPaused = !_flow.IsPaused;
        if (sender is NativeMenuItem item)
            item.Header = _flow.IsPaused ? Strings.TrayResume : Strings.TrayPause;
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
