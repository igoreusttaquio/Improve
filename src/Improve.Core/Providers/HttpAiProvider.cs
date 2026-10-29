using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Improve.Core.Models;

namespace Improve.Core.Providers;

/// <summary>Base comum: envio HTTP, timeout por inatividade, cancelamento e tradução de erros para <see cref="ProviderException"/>.</summary>
public abstract class HttpAiProvider(HttpClient http, ProviderConfig config, string? apiKey, TimeSpan? timeout) : IAiProvider
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    protected HttpClient Http { get; } = http;
    protected ProviderConfig Config { get; } = config;
    protected string? ApiKey { get; } = apiKey;

    /// <summary>Tempo máximo sem receber nada do provedor (o relógio reinicia a cada pedaço do streaming).</summary>
    protected TimeSpan Timeout { get; } = timeout ?? DefaultTimeout;

    public async IAsyncEnumerable<string> StreamAsync(AiRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        idle.CancelAfter(Timeout);
        var chunks = StreamCoreAsync(request, idle.Token).GetAsyncEnumerator(idle.Token);
        await using (chunks.ConfigureAwait(false))
        {
            while (true)
            {
                try
                {
                    if (!await chunks.MoveNextAsync().ConfigureAwait(false))
                        yield break;
                }
                catch (Exception ex) when (Translate(ex, ct) is { } translated)
                {
                    throw translated;
                }
                idle.CancelAfter(Timeout);
                if (chunks.Current.Length > 0)
                    yield return chunks.Current;
            }
        }
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        idle.CancelAfter(Timeout);
        try
        {
            var models = await ListModelsCoreAsync(idle.Token).ConfigureAwait(false);
            return [.. models.Order(StringComparer.Ordinal)];
        }
        catch (Exception ex) when (Translate(ex, ct) is { } translated)
        {
            throw translated;
        }
    }

    public async Task<ProviderCheck> TestAsync(CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            await ListModelsAsync(ct).ConfigureAwait(false);
            return new ProviderCheck(true, watch.Elapsed, null);
        }
        catch (ProviderException ex)
        {
            return new ProviderCheck(false, watch.Elapsed, ex.Message);
        }
    }

    protected abstract IAsyncEnumerable<string> StreamCoreAsync(AiRequest request, CancellationToken ct);

    protected abstract Task<IReadOnlyList<string>> ListModelsCoreAsync(CancellationToken ct);

    /// <summary>Acrescenta cabeçalhos de autenticação e de versão da API.</summary>
    protected abstract void Authorize(HttpRequestMessage request);

    protected Uri Endpoint(string path) => new(Config.BaseUrl.TrimEnd('/') + path);

    protected async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, JsonObject? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, Endpoint(path));
        Authorize(request);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(body is null ? "application/json" : "text/event-stream"));
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
            return response;

        using (response)
        {
            var detail = await ReadErrorMessageAsync(response, ct).ConfigureAwait(false);
            throw new ProviderException(ProviderException.KindFromStatus(response.StatusCode), Config.Name, (int)response.StatusCode, detail);
        }
    }

    protected ProviderException StreamError(ProviderErrorKind kind, string? detail) => new(kind, Config.Name, detail: detail);

    /// <summary>Mensagem de erro do corpo (<c>{"error": {"message": ...}}</c>, formato comum às duas APIs), resumida.</summary>
    protected static string? ErrorMessage(JsonNode? body)
    {
        var error = body is JsonObject root ? root["error"] : null;
        if (error is JsonObject inner)
            error = inner["message"];
        var message = error is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;
        if (string.IsNullOrWhiteSpace(message))
            return null;
        message = message.Trim();
        return message.Length <= 200 ? message : message[..200] + "…";
    }

    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ErrorMessage(JsonNode.Parse(text));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Converte falhas em <see cref="ProviderException"/>. Devolve <c>null</c> para o cancelamento pedido pelo usuário, que segue como está.</summary>
    private ProviderException? Translate(Exception ex, CancellationToken userToken) => ex switch
    {
        ProviderException p => p,
        OperationCanceledException when userToken.IsCancellationRequested => null,
        OperationCanceledException or TimeoutException => new(ProviderErrorKind.Unavailable, Config.Name, inner: ex),
        HttpRequestException { StatusCode: { } status } => new(ProviderException.KindFromStatus(status), Config.Name, (int)status, inner: ex),
        HttpRequestException or IOException => new(ProviderErrorKind.NoNetwork, Config.Name, inner: ex),
        JsonException or InvalidOperationException => new(ProviderErrorKind.Unknown, Config.Name, inner: ex),
        _ => null,
    };
}
