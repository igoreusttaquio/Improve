using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Improve.Core.Models;

namespace Improve.Core.Providers;

/// <summary>API Messages da Anthropic, com streaming SSE.</summary>
public sealed class AnthropicProvider(HttpClient http, ProviderConfig config, string? apiKey, TimeSpan? timeout = null)
    : HttpAiProvider(http, config, apiKey, timeout)
{
    public const string ApiVersion = "2023-06-01";

    protected override async IAsyncEnumerable<string> StreamCoreAsync(AiRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens,
            ["system"] = request.System,
            ["messages"] = new JsonArray([.. request.Messages.Select(m => new JsonObject
            {
                ["role"] = m.Role == ChatRole.User ? "user" : "assistant",
                ["content"] = m.Content,
            })]),
            ["stream"] = true,
        };
        if (request.Temperature is { } temperature)
            body["temperature"] = temperature;

        using var response = await SendAsync(HttpMethod.Post, "/v1/messages", body, ct).ConfigureAwait(false);
        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await foreach (var sse in SseReader.ReadAsync(stream, ct).ConfigureAwait(false))
        {
            var data = JsonNode.Parse(sse.Data);
            switch (sse.Event ?? data?["type"]?.GetValue<string>())
            {
                case "content_block_delta":
                    if (data?["delta"]?["text"] is JsonValue text)
                        yield return text.GetValue<string>();
                    break;
                case "error":
                    throw StreamError(KindFromErrorType(data?["error"]?["type"]?.GetValue<string>()), ErrorMessage(data));
                case "message_stop":
                    yield break;
            }
        }
    }

    protected override async Task<IReadOnlyList<string>> ListModelsCoreAsync(CancellationToken ct)
    {
        var models = new List<string>();
        string? afterId = null;
        do
        {
            var path = afterId is null ? "/v1/models?limit=1000" : $"/v1/models?limit=1000&after_id={Uri.EscapeDataString(afterId)}";
            using var response = await SendAsync(HttpMethod.Get, path, null, ct).ConfigureAwait(false);
            var page = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            models.AddRange(page?["data"]?.AsArray().Select(m => m?["id"]?.GetValue<string>()).OfType<string>() ?? []);
            afterId = page?["has_more"]?.GetValue<bool>() == true ? page["last_id"]?.GetValue<string>() : null;
        }
        while (afterId is not null);
        return models;
    }

    protected override void Authorize(HttpRequestMessage request)
    {
        if (string.IsNullOrEmpty(ApiKey))
            throw new ProviderException(ProviderErrorKind.MissingKey, Config.Name);
        request.Headers.Add("x-api-key", ApiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
    }

    private static ProviderErrorKind KindFromErrorType(string? type) => type switch
    {
        "authentication_error" or "permission_error" => ProviderErrorKind.InvalidKey,
        "rate_limit_error" => ProviderErrorKind.RateLimited,
        "overloaded_error" or "api_error" => ProviderErrorKind.Unavailable,
        "invalid_request_error" or "not_found_error" or "request_too_large" => ProviderErrorKind.BadRequest,
        _ => ProviderErrorKind.Unknown,
    };
}
