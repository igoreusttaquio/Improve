using System.Text.Json.Serialization;

namespace Improve.Core.Models;

/// <summary>O que fazer com o resultado da IA.</summary>
public enum OutputMode
{
    /// <summary>Substitui a seleção no app de origem.</summary>
    [JsonStringEnumMemberName("replace")]
    Replace,

    /// <summary>Mostra o resultado no painel de sugestão.</summary>
    [JsonStringEnumMemberName("suggest")]
    Suggest,

    /// <summary>Copia o resultado para a área de transferência.</summary>
    [JsonStringEnumMemberName("copy")]
    Copy,
}
