using System.Text.Json;
using System.Text.Json.Serialization;
using Improve.Core.Storage;

namespace Improve.Platform.Secrets;

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class SecretsJsonContext : JsonSerializerContext;

/// <summary>Arquivo JSON <c>providerId → valor</c>, gravado de forma atômica. O valor é o que a implementação decidir (cifrado ou não).</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001",
    Justification = "SemaphoreSlim só precisa de Dispose quando AvailableWaitHandle é usado.")]
internal sealed class SecretFile(string path)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        var values = await ReadAsync(ct).ConfigureAwait(false);
        return values.GetValueOrDefault(key);
    }

    public Task SetAsync(string key, string value, CancellationToken ct) => UpdateAsync(v => v[key] = value, ct);

    public Task DeleteAsync(string key, CancellationToken ct) => UpdateAsync(v => v.Remove(key), ct);

    private async Task UpdateAsync(Action<Dictionary<string, string>> change, CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var values = await ReadAsync(ct).ConfigureAwait(false);
            change(values);
            await AtomicFile.WriteAllBytesAsync(path, JsonSerializer.SerializeToUtf8Bytes(values, SecretsJsonContext.Default.DictionaryStringString), ct)
                .ConfigureAwait(false);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, string>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(path))
            return [];
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync(stream, SecretsJsonContext.Default.DictionaryStringString, ct).ConfigureAwait(false) ?? [];
    }
}
