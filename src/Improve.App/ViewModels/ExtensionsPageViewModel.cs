using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Improve.App.Resources;
using Improve.Core.Execution;
using Improve.Core.Models;
using Improve.Core.Providers;
using Improve.Core.Storage;

namespace Improve.App.ViewModels;

/// <summary>Linha da lista de extensões (ExtensionCard).</summary>
public sealed partial class ExtensionCardViewModel(Extension extension, string meta, Func<ExtensionCardViewModel, bool, Task> setEnabled) : ObservableObject
{
    public Extension Extension { get; } = extension;
    public string Name => Extension.Name;
    public string Icon => Extension.Icon;
    public string Meta { get; } = meta;
    public string ModeLabel => ModeText(Extension.Mode);

    [ObservableProperty]
    private bool _enabled = extension.Enabled;

    [ObservableProperty]
    private bool _isDragging;

    partial void OnEnabledChanged(bool value) => _ = setEnabled(this, value);

    public static string ModeText(OutputMode mode) => mode switch
    {
        OutputMode.Replace => Strings.ModeReplace,
        OutputMode.Copy => Strings.ModeCopy,
        _ => Strings.ModeSuggest,
    };
}

public sealed partial class ExtensionsPageViewModel : ObservableObject
{
    private readonly ConfigState _config;
    private readonly ISecretStore _secrets;
    private readonly IProviderFactory _providers;
    private readonly ExtensionGenerator _generator;
    private IReadOnlyList<Extension>? _pendingImport;

    public ExtensionsPageViewModel(ConfigState config, ISecretStore secrets, IProviderFactory providers, ExtensionGenerator generator)
    {
        _config = config;
        _secrets = secrets;
        _providers = providers;
        _generator = generator;
        _config.Changed += (_, _) => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    public ObservableCollection<ExtensionCardViewModel> Cards { get; } = [];
    public IReadOnlyList<ProviderConfig> Providers => _config.Settings.Providers;
    public bool IsEmpty => Cards.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    private ExtensionEditorViewModel? _editor;

    public bool IsEditing => Editor is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingImport))]
    private string? _pendingImportText;

    [ObservableProperty]
    private ProviderConfig? _importProvider;

    [ObservableProperty]
    private string? _error;

    public bool HasPendingImport => PendingImportText is not null;

    [RelayCommand]
    private void New() => Editor = CreateEditor(null);

    [RelayCommand]
    private void Edit(ExtensionCardViewModel card) => Editor = CreateEditor(card.Extension);

    /// <summary>Move durante o arraste, só na tela. A ordem é gravada em <see cref="CommitOrderAsync"/>.</summary>
    public void MoveLive(int from, int to)
    {
        if (from != to && from >= 0 && to >= 0 && from < Cards.Count && to < Cards.Count)
            Cards.Move(from, to);
    }

    public Task CommitOrderAsync()
    {
        var order = Cards.Select(c => c.Extension.Id).ToList();
        return _config.UpdateExtensionsAsync(list => list.OrderBy(e => order.IndexOf(e.Id)));
    }

    public byte[] Export() => ExtensionPorter.Export(_config.Extensions.Extensions.OrderBy(e => e.Order));

    /// <summary>Lê o arquivo e pede o provedor antes de adicionar.</summary>
    public void StartImport(byte[] data)
    {
        Error = null;
        try
        {
            _pendingImport = ExtensionPorter.Import(data, providerId: null, firstOrder: 0);
        }
        catch (InvalidDataException ex)
        {
            Error = ex.Message;
            return;
        }
        ImportProvider = Providers.Count > 0 ? Providers[0] : null;
        PendingImportText = string.Format(CultureInfo.CurrentCulture, Strings.ImportPending, _pendingImport.Count);
    }

    [RelayCommand]
    private async Task ConfirmImportAsync()
    {
        if (_pendingImport is not { } imported)
            return;
        var providerId = ImportProvider?.Id;
        CancelImport();
        await _config.UpdateExtensionsAsync(list => list.Concat(imported.Select(e => e with { ProviderId = providerId })));
    }

    [RelayCommand]
    private void CancelImport()
    {
        _pendingImport = null;
        PendingImportText = null;
    }

    private ExtensionEditorViewModel CreateEditor(Extension? extension) =>
        new(_config, _secrets, _providers, _generator, extension, () => Editor = null);

    private void Refresh()
    {
        var position = 0;
        Cards.Clear();
        foreach (var extension in _config.Extensions.Extensions.OrderBy(e => e.Order))
        {
            var provider = _config.Settings.Providers.FirstOrDefault(p => p.Id == extension.ProviderId)?.Name ?? Strings.CardNoProvider;
            var meta = extension.Enabled && ++position <= 9
                ? $"{provider} · {string.Format(CultureInfo.CurrentCulture, Strings.CardPopupKey, position)}"
                : provider;
            Cards.Add(new ExtensionCardViewModel(extension, meta, SetEnabledAsync));
        }
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Providers));
    }

    private Task SetEnabledAsync(ExtensionCardViewModel card, bool enabled) =>
        _config.UpdateExtensionsAsync(list => list.Select(e => e.Id == card.Extension.Id ? e with { Enabled = enabled } : e));
}
