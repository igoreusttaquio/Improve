using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Improve.App.Resources;
using Improve.Core.Models;
using Improve.Core.Storage;

namespace Improve.App.ViewModels;

public sealed record ThemeOption(ThemePreference Value, string Label)
{
    public static IReadOnlyList<ThemeOption> All { get; } =
    [
        new(ThemePreference.System, Strings.ThemeSystem),
        new(ThemePreference.Dark, Strings.ThemeDark),
        new(ThemePreference.Light, Strings.ThemeLight),
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
        _startWithSystem = config.Settings.StartWithSystem;
    }

    [ObservableProperty] private string? _globalHotkey;
    [ObservableProperty] private string? _hotkeyError;
    [ObservableProperty] private ThemeOption _theme;
    [ObservableProperty] private bool _startWithSystem;

    partial void OnGlobalHotkeyChanged(string? value)
    {
        HotkeyError = Validate(value, out var hotkey);
        if (HotkeyError is null && hotkey is not null)
            _ = _config.UpdateSettingsAsync(s => s with { GlobalHotkey = hotkey.ToString() });
    }

    partial void OnThemeChanged(ThemeOption value) => _ = _config.UpdateSettingsAsync(s => s with { Theme = value.Value });

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

public sealed class AboutPageViewModel : ObservableObject
{
    public string Version { get; } = string.Format(CultureInfo.CurrentCulture, Strings.AboutVersion,
        typeof(AboutPageViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?");

    public IReadOnlyList<LicenseInfo> Licenses { get; } =
    [
        new("Avalonia", "MIT"),
        new("CommunityToolkit.Mvvm", "MIT"),
        new("Microsoft.Extensions.*", "MIT"),
        new("Lucide", "ISC"),
        new("Inter", "SIL Open Font License 1.1"),
        new("JetBrains Mono", "SIL Open Font License 1.1"),
    ];
}
