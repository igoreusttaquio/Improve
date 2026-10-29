namespace Improve.Core.Storage;

/// <summary>Guarda as chaves de API fora dos arquivos JSON, indexadas pelo id do provedor.</summary>
public interface ISecretStore
{
    /// <summary><c>false</c> quando o armazenamento é um arquivo comum (fallback do Linux): a UI deve avisar.</summary>
    bool IsSecure { get; }

    Task<string?> GetAsync(string providerId, CancellationToken ct = default);
    Task SetAsync(string providerId, string secret, CancellationToken ct = default);
    Task DeleteAsync(string providerId, CancellationToken ct = default);
}
