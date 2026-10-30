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

public partial class App(IServiceProvider services, SingleInstance instance, string[] args) : Application
{
    /// <summary>Argumento usado pelo início automático com o sistema: abre só na bandeja.</summary>
    public const string BackgroundArgument = "--background";

    private SettingsWindow? _settings;
    private FlowController? _flow;
    private LanguagePreference _language;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var config = services.GetRequiredService<ConfigState>();
        var autostart = services.GetService<IAutostartService>();
        _language = config.Settings.Language; // já aplicado em AppServices, antes de carregar as extensões
        ApplySettings(config, autostart);
        config.Changed += (_, _) => Dispatcher.UIThread.Post(() => ApplySettings(config, autostart));

        // Integrações de SO (por enquanto só no Windows).
        _flow = services.GetService<FlowController>();
        if (_flow is not null)
        {
            _flow.OpenSettings = OpenSettings;
            _flow.Start();
        }
        instance.Listen(args => Dispatcher.UIThread.Post(() => OnSecondInstance(args)));

        // Aberto pelo usuário: mostra as configurações (senão parece que nada abriu). Pelo autostart: só a bandeja.
        if (!args.Contains(BackgroundArgument))
            OpenSettings(createExtension: false);

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
        Resources["CodeFontSize"] = Math.Clamp(config.Settings.CodeFontSize, 9, 24);
        if (autostart is not null && autostart.IsEnabled() != config.Settings.StartWithSystem)
            autostart.SetEnabled(config.Settings.StartWithSystem);
        if (config.Settings.Language != _language)
            ApplyLanguage(config.Settings.Language);
    }

    // As strings vêm de x:Static, lidas ao criar cada tela: troca a cultura, refaz o menu da bandeja e
    // reabre a janela de configurações na mesma página.
    private void ApplyLanguage(LanguagePreference language)
    {
        _language = language;
        Localization.Apply(language);
        UpdateTrayMenu();
        if (_settings is not { DataContext: SettingsViewModel old } window)
            return;
        var page = old.Pages.ToList().IndexOf(old.SelectedPage);
        window.Close();
        OnOpenSettings(null, EventArgs.Empty);
        var current = (SettingsViewModel)_settings!.DataContext!;
        current.SelectedPage = current.Pages[Math.Max(0, page)];
    }

    private void UpdateTrayMenu()
    {
        if (TrayIcon.GetIcons(this) is not [{ Menu: { } menu }, ..])
            return;
        var items = menu.Items.OfType<NativeMenuItem>().ToList();
        items[0].Header = Strings.TrayOpenSettings;
        items[1].Header = _flow?.IsPaused == true ? Strings.TrayResume : Strings.TrayPause;
        items[2].Header = Strings.TrayQuit;
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

    private void OnOpenSettings(object? sender, EventArgs e) => OpenSettings(createExtension: false);

    /// <summary>
    /// Abre (ou traz para frente) as configurações; com <paramref name="createExtension"/>, no editor de nova
    /// extensão; com <paramref name="extensionId"/>, no editor daquela extensão.
    /// </summary>
    private void OpenSettings(bool createExtension, Guid? extensionId = null)
    {
        if (_settings is null)
        {
            _settings = new SettingsWindow(services.GetRequiredService<SettingsViewModel>());
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show();
        _settings.Activate();
        if (_settings.TryGetPlatformHandle()?.Handle is { } handle)
            services.GetService<IWindowActivator>()?.Activate(handle);

        if (_settings.DataContext is not SettingsViewModel { Pages: [{ Page: ExtensionsPageViewModel extensions } page, ..] } vm)
            return;
        if (createExtension)
        {
            vm.SelectedPage = page;
            extensions.NewCommand.Execute(null);
        }
        else if (extensions.Cards.FirstOrDefault(c => c.Extension.Id == extensionId) is { } card)
        {
            vm.SelectedPage = page;
            extensions.EditCommand.Execute(card);
        }
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
