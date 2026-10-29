using System.Text.Json;
using Improve.Core.Models;

namespace Improve.Core.Storage;

/// <summary>Exporta e importa extensões como <c>.json</c>. O provedor não vai no arquivo: na importação o usuário escolhe.</summary>
public static class ExtensionPorter
{
    public static byte[] Export(IEnumerable<Extension> extensions)
    {
        var document = new ExtensionsDocument
        {
            Extensions = [.. extensions.Select((e, i) => e with { ProviderId = null, Hotkey = null, Order = i })],
        };
        return JsonSerializer.SerializeToUtf8Bytes(document, CoreJsonContext.Default.ExtensionsDocument);
    }

    /// <summary>
    /// Lê um arquivo exportado e devolve as extensões com ids novos, ligadas ao provedor escolhido e
    /// posicionadas depois de <paramref name="firstOrder"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">O arquivo não é uma exportação do improve.</exception>
    public static IReadOnlyList<Extension> Import(ReadOnlySpan<byte> json, string? providerId, int firstOrder)
    {
        ExtensionsDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json, CoreJsonContext.Default.ExtensionsDocument);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(Resources.CoreStrings.ImportInvalidFile, ex);
        }
        if (document is null || document.Extensions.Count == 0 || document.Version > ExtensionsDocument.CurrentVersion)
            throw new InvalidDataException(Resources.CoreStrings.ImportInvalidFile);

        return [.. document.Extensions.Select((e, i) => e with
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            Hotkey = null,
            Order = firstOrder + i,
        })];
    }
}
