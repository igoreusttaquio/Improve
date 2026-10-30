using CommunityToolkit.Mvvm.ComponentModel;
using Improve.App.Resources;
using Improve.App.Services;
using Improve.Core.Execution;
using Improve.Core.Providers;
using Improve.Core.Storage;
using Improve.Platform;

namespace Improve.App.ViewModels;

public sealed record NavItem(string Title, string Icon, ObservableObject Page);

/// <summary>Janela de configurações: navegação lateral entre as páginas.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel(ConfigState config, ISecretStore secrets, IProviderFactory providers, ExtensionGenerator generator, AppUpdater updater,
        IElevationService? elevation = null)
    {
        Pages =
        [
            new(Strings.NavExtensions, "blocks", new ExtensionsPageViewModel(config, secrets, providers, generator)),
            new(Strings.NavProviders, "plug", new ProvidersPageViewModel(config, secrets, providers)),
            new(Strings.NavGeneral, "settings", new GeneralPageViewModel(config, elevation)),
            new(Strings.NavAbout, "info", new AboutPageViewModel(updater)),
        ];
        _selectedPage = Pages[0];
    }

    public IReadOnlyList<NavItem> Pages { get; }

    [ObservableProperty]
    private NavItem _selectedPage;
}
