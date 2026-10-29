using System.Runtime.Versioning;
using Improve.Core.Storage;

namespace Improve.Platform.Secrets;

/// <summary>
/// macOS: Keychain pelo CLI <c>security</c> (primeiro passo; trocar por Security.framework via P/Invoke).
/// A chave aparece por um instante nos argumentos do processo ao gravar; ver docs/decisoes.md.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class KeychainSecretStore : ISecretStore
{
    private const string Service = "improve";
    private const string Security = "/usr/bin/security";

    public bool IsSecure => true;

    public async Task<string?> GetAsync(string providerId, CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync(Security, ["find-generic-password", "-s", Service, "-a", providerId, "-w"], null, ct)
            .ConfigureAwait(false);
        return result is (0, var output) ? output.TrimEnd('\n') : null;
    }

    public async Task SetAsync(string providerId, string secret, CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync(Security, ["add-generic-password", "-U", "-s", Service, "-a", providerId, "-w", secret], null, ct)
            .ConfigureAwait(false);
        if (result is not (0, _))
            throw new IOException("Não foi possível gravar a chave no Keychain.");
    }

    public Task DeleteAsync(string providerId, CancellationToken ct = default) =>
        ProcessRunner.RunAsync(Security, ["delete-generic-password", "-s", Service, "-a", providerId], null, ct);
}
