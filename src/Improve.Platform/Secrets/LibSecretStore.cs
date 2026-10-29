using System.Runtime.Versioning;
using Improve.Core.Storage;

namespace Improve.Platform.Secrets;

/// <summary>Linux: libsecret pelo CLI <c>secret-tool</c>. A chave vai pelo stdin, nunca pelos argumentos.</summary>
[SupportedOSPlatform("linux")]
public sealed class LibSecretStore : ISecretStore
{
    private const string Tool = "secret-tool";

    public bool IsSecure => true;

    /// <summary>Confere se o <c>secret-tool</c> existe e se o serviço de segredos responde.</summary>
    public static async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync(Tool, ["search", "service", "improve-probe"], null, ct).ConfigureAwait(false);
        return result is (0 or 1, _);
    }

    public async Task<string?> GetAsync(string providerId, CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync(Tool, ["lookup", "service", "improve", "account", providerId], null, ct).ConfigureAwait(false);
        return result is (0, var output) && output.Length > 0 ? output.TrimEnd('\n') : null;
    }

    public async Task SetAsync(string providerId, string secret, CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync(Tool, ["store", $"--label=improve: {providerId}", "service", "improve", "account", providerId], secret, ct)
            .ConfigureAwait(false);
        if (result is not (0, _))
            throw new IOException("Não foi possível gravar a chave no serviço de segredos.");
    }

    public Task DeleteAsync(string providerId, CancellationToken ct = default) =>
        ProcessRunner.RunAsync(Tool, ["clear", "service", "improve", "account", providerId], null, ct);
}

/// <summary>Fallback do Linux sem serviço de segredos: arquivo com permissão 600, sem cifra. A UI deve avisar.</summary>
public sealed class PlainFileSecretStore(string path) : ISecretStore
{
    private readonly SecretFile _file = new(path);

    public bool IsSecure => false;

    public Task<string?> GetAsync(string providerId, CancellationToken ct = default) => _file.GetAsync(providerId, ct);
    public Task SetAsync(string providerId, string secret, CancellationToken ct = default) => _file.SetAsync(providerId, secret, ct);
    public Task DeleteAsync(string providerId, CancellationToken ct = default) => _file.DeleteAsync(providerId, ct);
}
