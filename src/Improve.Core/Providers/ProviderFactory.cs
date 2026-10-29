using Improve.Core.Models;

namespace Improve.Core.Providers;

public interface IProviderFactory
{
    IAiProvider Create(ProviderConfig config, string? apiKey);
}

/// <summary>Cria provedores sobre um único <see cref="HttpClient"/> compartilhado.</summary>
public sealed class ProviderFactory(HttpClient http, TimeSpan? timeout = null) : IProviderFactory
{
    public IAiProvider Create(ProviderConfig config, string? apiKey) => config.Kind switch
    {
        ProviderKind.Anthropic => new AnthropicProvider(http, config, apiKey, timeout),
        ProviderKind.OpenAi => new OpenAiCompatibleProvider(http, config, apiKey, timeout),
        _ => throw new ArgumentOutOfRangeException(nameof(config), config.Kind, null),
    };

    /// <summary>HttpClient sem timeout próprio: o limite é o de inatividade de cada provedor.</summary>
    public static HttpClient CreateHttpClient() => new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan,
    };
}
