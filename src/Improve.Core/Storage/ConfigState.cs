using Improve.Core.Models;

namespace Improve.Core.Storage;

/// <summary>Configurações e extensões em memória, compartilhadas pela UI. Toda alteração é gravada em disco.</summary>
public sealed class ConfigState(SettingsStore store)
{
    public AppSettings Settings { get; private set; } = AppSettings.CreateDefault();
    public ExtensionsDocument Extensions { get; private set; } = new();

    /// <summary>Extensões ativas, na ordem do popup.</summary>
    public IReadOnlyList<Extension> EnabledExtensions => [.. Extensions.Extensions.Where(e => e.Enabled).OrderBy(e => e.Order)];

    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Settings = await store.LoadSettingsAsync(ct).ConfigureAwait(false);
        Extensions = await store.LoadExtensionsAsync(ct).ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpdateSettingsAsync(Func<AppSettings, AppSettings> change, CancellationToken ct = default)
    {
        var updated = change(Settings);
        await store.SaveSettingsAsync(updated, ct).ConfigureAwait(false);
        Settings = updated;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Recebe a lista atual e devolve a nova; a ordem da lista vira o campo <c>order</c>.</summary>
    public async Task UpdateExtensionsAsync(Func<IReadOnlyList<Extension>, IEnumerable<Extension>> change, CancellationToken ct = default)
    {
        var ordered = Extensions.Extensions.OrderBy(e => e.Order).ToList();
        var updated = Extensions with { Extensions = [.. change(ordered).Select((e, i) => e with { Order = i })] };
        await store.SaveExtensionsAsync(updated, ct).ConfigureAwait(false);
        Extensions = updated;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
