using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Improve.Core.Updates;

/// <summary>Versão publicada mais nova que a instalada, com o instalador da plataforma atual.</summary>
public sealed record UpdateInfo(Version Version, string Tag, Uri DownloadUrl, string FileName, long Size);

/// <summary>Consulta a última release do GitHub e baixa o instalador.</summary>
public sealed class UpdateChecker(HttpClient http, string repository = UpdateChecker.DefaultRepository)
{
    public const string DefaultRepository = "igoreusttaquio/Improve";

    /// <summary>Devolve a atualização, ou <c>null</c> se a instalada já é a mais nova (ou não há instalador para esta plataforma).</summary>
    public async Task<UpdateInfo?> CheckAsync(Version current, string assetName, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://api.github.com/repos/{repository}/releases/latest"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("improve", current.ToString()));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        // Sem autenticação, a API permite 60 consultas por hora por IP; redes corporativas esgotam isso fácil.
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            return await CheckByRedirectAsync(current, assetName, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var release = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));

        var tag = release?["tag_name"]?.GetValue<string>();
        if (tag is null || !TryParseVersion(tag, out var latest) || latest <= Normalize(current))
            return null;

        foreach (var asset in release!["assets"]?.AsArray() ?? [])
        {
            if (asset?["name"]?.GetValue<string>() == assetName && asset["browser_download_url"]?.GetValue<string>() is { } url)
                return new UpdateInfo(latest, tag, new Uri(url), assetName, asset["size"]?.GetValue<long>() ?? 0);
        }
        return null;
    }

    // Plano B sem limite de consultas: github.com/<repo>/releases/latest redireciona para /releases/tag/<tag>.
    private async Task<UpdateInfo?> CheckByRedirectAsync(Version current, string assetName, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://github.com/{repository}/releases/latest"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("improve", current.ToString()));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var tag = Uri.UnescapeDataString(response.RequestMessage?.RequestUri?.Segments[^1] ?? "");
        if (!TryParseVersion(tag, out var latest) || latest <= Normalize(current))
            return null;
        return new UpdateInfo(latest, tag, new Uri($"https://github.com/{repository}/releases/download/{tag}/{assetName}"), assetName, 0);
    }

    /// <summary>Baixa o instalador para <paramref name="directory"/>, informando o progresso de 0 a 1.</summary>
    public async Task<string> DownloadAsync(UpdateInfo update, string directory, IProgress<double>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, update.FileName);
        using var request = new HttpRequestMessage(HttpMethod.Get, update.DownloadUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("improve", update.Version.ToString()));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? update.Size;

        // Baixa para um arquivo temporário e só renomeia no fim, para nunca executar um instalador pela metade.
        var partial = path + ".part";
        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var target = File.Create(partial))
        {
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                read += n;
                if (total > 0)
                    progress?.Report(Math.Min(1, (double)read / total));
            }
        }
        File.Move(partial, path, overwrite: true);
        return path;
    }

    /// <summary>Aceita <c>v1.2.3</c> ou <c>1.2.3</c>; sufixos como <c>-beta</c> são ignorados.</summary>
    public static bool TryParseVersion(string text, out Version version)
    {
        var core = text.TrimStart('v', 'V').Split('-', '+')[0];
        if (Version.TryParse(core, out var parsed))
        {
            version = Normalize(parsed);
            return true;
        }
        version = new Version(0, 0, 0);
        return false;
    }

    // 1.2 e 1.2.0 devem ser iguais; Version trata componentes ausentes como -1.
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
