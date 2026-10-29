namespace Improve.Core.Models;

/// <summary>Um prompt de IA com ícone, aplicado à seleção pelo popup.</summary>
public sealed record Extension
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }

    /// <summary>Nome do ícone Lucide (ex.: <c>wand-sparkles</c>), não o SVG.</summary>
    public string Icon { get; init; } = "wand-sparkles";

    public required string Prompt { get; init; }
    public string? ProviderId { get; init; }

    /// <summary>ID escolhido na lista do provedor. Vazio até o usuário escolher.</summary>
    public string Model { get; init; } = "";

    public OutputMode Mode { get; init; } = OutputMode.Suggest;
    public double? Temperature { get; init; }

    /// <summary>Atalho direto opcional, que pula o popup.</summary>
    public string? Hotkey { get; init; }

    public bool Enabled { get; init; } = true;
    public int Order { get; init; }
}
