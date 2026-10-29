namespace Improve.Core.Providers;

public interface IAiProvider
{
    /// <summary>Pedaços de texto do resultado, na ordem em que chegam.</summary>
    IAsyncEnumerable<string> StreamAsync(AiRequest request, CancellationToken ct);

    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct);

    /// <summary>Confere a chave e mede a latência, sem gastar tokens (lista os modelos).</summary>
    Task<ProviderCheck> TestAsync(CancellationToken ct);
}
