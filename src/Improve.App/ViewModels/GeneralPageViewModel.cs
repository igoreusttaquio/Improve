using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Improve.App.Services;
using Improve.App.Resources;
using Improve.Core.Models;
using Improve.Core.Storage;
using Improve.Core.Updates;

namespace Improve.App.ViewModels;

public sealed record ThemeOption(ThemePreference Value, string Label)
{
    public static IReadOnlyList<ThemeOption> All =>
    [
        new(ThemePreference.System, Strings.ThemeSystem),
        new(ThemePreference.Dark, Strings.ThemeDark),
        new(ThemePreference.Light, Strings.ThemeLight),
    ];
}

public sealed record LanguageOption(LanguagePreference Value, string Label)
{
    // Os idiomas aparecem no próprio nome nativo; só "seguir o sistema" é traduzido.
    public static IReadOnlyList<LanguageOption> All =>
    [
        new(LanguagePreference.System, Strings.LanguageSystem),
        new(LanguagePreference.PortugueseBrazil, "Português (Brasil)"),
        new(LanguagePreference.English, "English"),
        new(LanguagePreference.Spanish, "Español"),
    ];
}

/// <summary>Página Geral: grava cada alteração na hora.</summary>
public sealed partial class GeneralPageViewModel : ObservableObject
{
    private readonly ConfigState _config;

    public GeneralPageViewModel(ConfigState config)
    {
        _config = config;
        _globalHotkey = config.Settings.GlobalHotkey;
        _theme = ThemeOption.All.First(t => t.Value == config.Settings.Theme);
        _language = LanguageOption.All.First(l => l.Value == config.Settings.Language);
        _startWithSystem = config.Settings.StartWithSystem;
    }

    [ObservableProperty] private string? _globalHotkey;
    [ObservableProperty] private string? _hotkeyError;
    [ObservableProperty] private ThemeOption _theme;
    [ObservableProperty] private LanguageOption _language;
    [ObservableProperty] private bool _startWithSystem;


    partial void OnGlobalHotkeyChanged(string? value)
    {
        HotkeyError = Validate(value, out var hotkey);
        if (HotkeyError is null && hotkey is not null)
            _ = _config.UpdateSettingsAsync(s => s with { GlobalHotkey = hotkey.ToString() });
    }

    partial void OnThemeChanged(ThemeOption value) => _ = _config.UpdateSettingsAsync(s => s with { Theme = value.Value });

    partial void OnLanguageChanged(LanguageOption value) => _ = _config.UpdateSettingsAsync(s => s with { Language = value.Value });

    partial void OnStartWithSystemChanged(bool value) => _ = _config.UpdateSettingsAsync(s => s with { StartWithSystem = value });

    private string? Validate(string? text, out Hotkey? hotkey)
    {
        hotkey = null;
        if (!Hotkey.TryParse(text, out var parsed) || !parsed.IsValid)
            return Strings.HotkeyInvalid;
        hotkey = parsed;
        var owner = HotkeyConflicts.ForGlobal(parsed, _config.Extensions.Extensions);
        return owner is null ? null : string.Format(CultureInfo.CurrentCulture, Strings.HotkeyConflict, owner);
    }
}

public sealed record LicenseInfo(string Component, string License);

public sealed partial class AboutPageViewModel(AppUpdater updater) : ObservableObject
{
    public string Version { get; } = string.Format(CultureInfo.CurrentCulture, Strings.AboutVersion,
        typeof(AboutPageViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?");

    private UpdateInfo? _update;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand), nameof(InstallUpdateCommand))]
    private bool _isBusy;

    [ObservableProperty] private string? _updateStatus;
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private double _downloadProgress;

    private bool CanRun => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task CheckForUpdatesAsync()
    {
        if (AppUpdater.AssetName is null)
        {
            UpdateStatus = Strings.UpdateUnsupported;
            return;
        }
        IsBusy = true;
        IsUpdateAvailable = false;
        UpdateStatus = Strings.UpdateChecking;
        try
        {
            _update = await updater.CheckAsync(CancellationToken.None);
            IsUpdateAvailable = _update is not null;
            UpdateStatus = _update is null
                ? Strings.UpdateUpToDate
                : string.Format(CultureInfo.CurrentCulture, Strings.UpdateAvailable, _update.Version);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            UpdateStatus = Strings.UpdateFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task InstallUpdateAsync()
    {
        if (_update is null)
            return;
        IsBusy = true;
        IsDownloading = true;
        DownloadProgress = 0;
        UpdateStatus = string.Format(CultureInfo.CurrentCulture, Strings.UpdateDownloading, _update.Version);
        try
        {
            var installer = await updater.DownloadAsync(_update, new Progress<double>(p => DownloadProgress = p * 100), CancellationToken.None);
            UpdateStatus = Strings.UpdateClosing;
            AppUpdater.InstallAndExit(installer);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            UpdateStatus = Strings.UpdateFailed;
            IsBusy = false;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    public IReadOnlyList<LicenseInfo> Licenses { get; } =
    [
        new("Avalonia", "MIT"),
        new("AvaloniaEdit / AvaloniaEdit.TextMate", "MIT"),
        new("TextMateSharp (gramáticas do VS Code)", "MIT"),
        new("SharpHook", "MIT"),
        new("libuiohook (usada pelo SharpHook)", "LGPL 3.0"),
        new("CommunityToolkit.Mvvm", "MIT"),
        new("Microsoft.Extensions.* / System.Security.Cryptography.ProtectedData", "MIT"),
        new(".NET", "MIT"),
        new("Lucide", "ISC"),
        new("Inter", "SIL Open Font License 1.1"),
        new("JetBrains Mono", "SIL Open Font License 1.1"),
    ];
}
