using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Improve.Core.Models;

namespace Improve.Core.Providers;

/// <summary>API de chat compatível com a OpenAI (OpenAI, DeepSeek, Ollama), com streaming SSE.</summary>
public sealed class OpenAiCompatibleProvider(HttpClient http, ProviderConfig config, string? apiKey, TimeSpan? timeout = null)
    : HttpAiProvider(http, config, apiKey, timeout)
{
    protected override async IAsyncEnumerable<string> StreamCoreAsync(AiRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var messages = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = request.System });
        foreach (var m in request.Messages)
            messages.Add(new JsonObject { ["role"] = m.Role == ChatRole.User ? "user" : "assistant", ["content"] = m.Content });

        // Sem limite de tokens: modelos de raciocínio da OpenAI rejeitam "max_tokens" (ver docs/decisoes.md).
        var body = new JsonObject { ["model"] = request.Model, ["messages"] = messages, ["stream"] = true };
        if (request.Temperature is { } temperature)
            body["temperature"] = temperature;

        using var response = await SendAsync(HttpMethod.Post, "/chat/completions", body, ct).ConfigureAwait(false);
        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await foreach (var sse in SseReader.ReadAsync(stream, ct).ConfigureAwait(false))
        {
            if (sse.Data == "[DONE]")
                yield break;
            var data = JsonNode.Parse(sse.Data);
            if (data?["error"] is not null)
                throw StreamError(ProviderErrorKind.BadRequest, ErrorMessage(data));
            if (data?["choices"]?[0]?["delta"]?["content"] is JsonValue content && content.TryGetValue<string>(out var text))
                yield return text;
        }
    }

    protected override async Task<IReadOnlyList<string>> ListModelsCoreAsync(CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, "/models", null, ct).ConfigureAwait(false);
        var page = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return [.. page?["data"]?.AsArray().Select(m => m?["id"]?.GetValue<string>()).OfType<string>() ?? []];
    }

    // A chave é opcional: o Ollama local não usa.
    protected override void Authorize(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
    }
}
