using System.Text.Json.Serialization;

namespace Improve.Core.Models;

/// <summary>Formato de API do provedor.</summary>
public enum ProviderKind
{
    [JsonStringEnumMemberName("anthropic")]
    Anthropic,

    /// <summary>Compatível com a API de chat da OpenAI (OpenAI, DeepSeek, Ollama).</summary>
    [JsonStringEnumMemberName("openai")]
    OpenAi,
}

/// <summary>Um provedor configurado. A chave de API nunca fica aqui: vai para o <c>ISecretStore</c>.</summary>
public sealed record ProviderConfig(string Id, ProviderKind Kind, string Name, string BaseUrl);
