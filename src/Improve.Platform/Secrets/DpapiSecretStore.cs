using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Improve.Core.Storage;

namespace Improve.Platform.Secrets;

/// <summary>Windows: chaves cifradas com DPAPI (escopo do usuário atual) num arquivo próprio.</summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretStore(string path) : ISecretStore
{
    private static readonly byte[] Entropy = "improve.secrets.v1"u8.ToArray();
    private readonly SecretFile _file = new(path);

    public bool IsSecure => true;

    public async Task<string?> GetAsync(string providerId, CancellationToken ct = default)
    {
        var stored = await _file.GetAsync(providerId, ct).ConfigureAwait(false);
        if (stored is null)
            return null;
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(stored), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Cifrado por outro usuário/máquina ou corrompido: trata como sem chave.
            return null;
        }
    }

    public Task SetAsync(string providerId, string secret, CancellationToken ct = default) =>
        _file.SetAsync(providerId, Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser)), ct);

    public Task DeleteAsync(string providerId, CancellationToken ct = default) => _file.DeleteAsync(providerId, ct);
}
