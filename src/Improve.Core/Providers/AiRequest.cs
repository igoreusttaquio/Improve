namespace Improve.Core.Providers;

public enum ChatRole
{
    User,
    Assistant,
}

public sealed record ChatMessage(ChatRole Role, string Content);

public sealed record AiRequest(
    string Model,
    string System,
    IReadOnlyList<ChatMessage> Messages,
    double? Temperature,
    int MaxTokens);

/// <summary>Resultado de "Testar conexão".</summary>
public sealed record ProviderCheck(bool Ok, TimeSpan Latency, string? ErrorMessage);
