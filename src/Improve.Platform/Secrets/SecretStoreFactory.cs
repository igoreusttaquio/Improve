using Improve.Core.Storage;

namespace Improve.Platform.Secrets;

public static class SecretStoreFactory
{
    /// <summary>Escolhe o armazenamento de chaves do SO atual.</summary>
    public static async Task<ISecretStore> CreateAsync(string configDirectory, CancellationToken ct = default)
    {
        if (OperatingSystem.IsWindows())
            return new DpapiSecretStore(Path.Combine(configDirectory, "secrets.dat"));
        if (OperatingSystem.IsMacOS())
            return new KeychainSecretStore();
        if (OperatingSystem.IsLinux() && await LibSecretStore.IsAvailableAsync(ct).ConfigureAwait(false))
            return new LibSecretStore();
        return new PlainFileSecretStore(Path.Combine(configDirectory, "secrets.json"));
    }
}
