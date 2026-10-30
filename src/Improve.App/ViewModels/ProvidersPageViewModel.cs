using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Improve.App.Resources;
using Improve.Core.Models;
using Improve.Core.Providers;
using Improve.Core.Storage;

namespace Improve.App.ViewModels;

public sealed record ProviderCardViewModel(ProviderConfig Config, string Meta, bool HasKey)
{
    public string Name => Config.Name;
    public string Icon => Config.Kind == ProviderKind.Anthropic ? "sparkle" : "server";
    public string KeyLabel => HasKey ? Strings.ProviderKeySaved : Strings.ProviderKeyMissing;
}

public sealed record KindOption(ProviderKind Kind, string Label, string DefaultUrl)
{
    public static IReadOnlyList<KindOption> All =>
    [
        new(ProviderKind.Anthropic, Strings.KindAnthropic, "https://api.anthropic.com"),
        new(ProviderKind.OpenAi, Strings.KindOpenAi, "https://api.openai.com/v1"),
    ];
}

public sealed partial class ProvidersPageViewModel : ObservableObject
{
    private readonly ConfigState _config;
    private readonly ISecretStore _secrets;
    private readonly IProviderFactory _providers;

    public ProvidersPageViewModel(ConfigState config, ISecretStore secrets, IProviderFactory providers)
    {
        _config = config;
        _secrets = secrets;
        _providers = providers;
        _config.Changed += (_, _) => Dispatcher.UIThread.Post(() => _ = RefreshAsync());
        _ = RefreshAsync();
    }

    public ObservableCollection<ProviderCardViewModel> Cards { get; } = [];
    public bool IsKeyStorageInsecure => !_secrets.IsSecure;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    private ProviderEditorViewModel? _editor;

    public bool IsEditing => Editor is not null;

    [RelayCommand]
    private void Add() => Editor = new ProviderEditorViewModel(_config, _secrets, _providers, null, () => Editor = null);

    [RelayCommand]
    private void Edit(ProviderCardViewModel card) =>
        Editor = new ProviderEditorViewModel(_config, _secrets, _providers, card.Config, () => Editor = null);

    private async Task RefreshAsync()
    {
        var cards = new List<ProviderCardViewModel>();
        foreach (var provider in _config.Settings.Providers)
        {
            var usage = _config.Extensions.Extensions.Count(e => e.ProviderId == provider.Id);
            var meta = $"{provider.BaseUrl} · {string.Format(CultureInfo.CurrentCulture, Strings.ProviderInUse, usage)}";
            cards.Add(new ProviderCardViewModel(provider, meta, await _secrets.GetAsync(provider.Id) is not null));
        }
        Cards.Clear();
        foreach (var card in cards)
            Cards.Add(card);
    }
}

public sealed partial class ProviderEditorViewModel : ObservableObject
{
    private readonly ConfigState _config;
    private readonly ISecretStore _secrets;
    private readonly IProviderFactory _providers;
    private readonly ProviderConfig? _original;
    private readonly Action _close;

    public ProviderEditorViewModel(ConfigState config, ISecretStore secrets, IProviderFactory providers, ProviderConfig? original, Action close)
    {
        _config = config;
        _secrets = secrets;
        _providers = providers;
        _original = original;
        _close = close;
        _kind = KindOption.All.First(k => k.Kind == (original?.Kind ?? ProviderKind.Anthropic));
        _name = original?.Name ?? "";
        _baseUrl = original?.BaseUrl ?? _kind.DefaultUrl;
        _ = LoadKeyStateAsync();
    }

    public bool IsNew => _original is null;
    public string Title => IsNew ? Strings.ProviderNew : _original!.Name;
    public bool IsKeyStorageInsecure => !_secrets.IsSecure;

    [ObservableProperty] private string _name;
    [ObservableProperty] private KindOption _kind;
    [ObservableProperty] private string _baseUrl;
    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private string? _keyStatus;
    [ObservableProperty] private string? _testMessage;
    [ObservableProperty] private bool _testOk;
    [ObservableProperty] private string? _validationError;

    // Ao trocar o tipo, troca a URL se ela ainda for a padrão do tipo anterior.
    partial void OnKindChanged(KindOption? oldValue, KindOption newValue)
    {
        if (oldValue is null || string.IsNullOrWhiteSpace(BaseUrl) || BaseUrl == oldValue.DefaultUrl)
            BaseUrl = newValue.DefaultUrl;
    }

    [RelayCommand]
    private async Task TestConnectionAsync(CancellationToken ct)
    {
        TestMessage = null;
        if (Validate() is { } error)
        {
            TestMessage = error;
            TestOk = false;
            return;
        }
        var key = ApiKey.Length > 0 ? ApiKey.Trim() : _original is null ? null : await _secrets.GetAsync(_original.Id, ct);
        var check = await _providers.Create(BuildConfig(_original?.Id ?? "teste"), key).TestAsync(ct);
        TestOk = check.Ok;
        TestMessage = check.Ok
            ? string.Format(CultureInfo.CurrentCulture, Strings.TestConnectionOk, (int)check.Latency.TotalMilliseconds)
            : check.ErrorMessage;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidationError = Validate();
        if (ValidationError is not null)
            return;
        var config = BuildConfig(_original?.Id ?? NewId());
        if (ApiKey.Trim() is { Length: > 0 } key)
            await _secrets.SetAsync(config.Id, key);
        await _config.UpdateSettingsAsync(s => s with
        {
            Providers = IsNew ? [.. s.Providers, config] : [.. s.Providers.Select(p => p.Id == config.Id ? config : p)],
        });
        _close();
    }

    [RelayCommand]
    private void Cancel() => _close();

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (_original is { } original)
        {
            await _secrets.DeleteAsync(original.Id);
            await _config.UpdateSettingsAsync(s => s with { Providers = [.. s.Providers.Where(p => p.Id != original.Id)] });
        }
        _close();
    }

    private async Task LoadKeyStateAsync()
    {
        var hasKey = _original is not null && await _secrets.GetAsync(_original.Id) is not null;
        KeyStatus = hasKey ? Strings.ApiKeyStored : Strings.ApiKeyMissing;
    }

    private string? Validate() =>
        string.IsNullOrWhiteSpace(Name) ? Strings.ProviderNameRequired
        : Uri.TryCreate(BaseUrl.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? null
        : Strings.ProviderUrlInvalid;

    private ProviderConfig BuildConfig(string id) => new(id, Kind.Kind, Name.Trim(), BaseUrl.Trim());

    private string NewId()
    {
        var prefix = Kind.Kind == ProviderKind.Anthropic ? "anthropic" : "openai";
        var ids = _config.Settings.Providers.Select(p => p.Id).ToHashSet();
        var n = 1;
        while (ids.Contains($"{prefix}-{n}"))
            n++;
        return $"{prefix}-{n}";
    }
}
