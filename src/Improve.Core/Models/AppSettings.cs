using System.Text.Json.Serialization;
using Improve.Core.Resources;

namespace Improve.Core.Models;

public enum ThemePreference
{
    [JsonStringEnumMemberName("system")]
    System,
    [JsonStringEnumMemberName("dark")]
    Dark,
    [JsonStringEnumMemberName("light")]
    Light,
}

/// <summary>Conteúdo de <c>settings.json</c>.</summary>
public sealed record AppSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public string GlobalHotkey { get; init; } = "Ctrl+Shift+Space";
    public ThemePreference Theme { get; init; } = ThemePreference.System;
    public LanguagePreference Language { get; init; } = LanguagePreference.System;
    public bool StartWithSystem { get; init; } = true;

    public IReadOnlyList<ProviderConfig> Providers { get; init; } = [];

    public static AppSettings CreateDefault() => new()
    {
        GlobalHotkey = OperatingSystem.IsMacOS() ? "Cmd+Shift+Space" : "Ctrl+Shift+Space",
        Providers =
        [
            new("anthropic-1", ProviderKind.Anthropic, "Anthropic", "https://api.anthropic.com"),
            new("openai-1", ProviderKind.OpenAi, "OpenAI", "https://api.openai.com/v1"),
            new("deepseek-1", ProviderKind.OpenAi, "DeepSeek", "https://api.deepseek.com/v1"),
        ],
    };
}

/// <summary>Conteúdo de <c>extensions.json</c>.</summary>
public sealed record ExtensionsDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public IReadOnlyList<Extension> Extensions { get; init; } = [];

    /// <summary>As 4 extensões de exemplo da primeira execução, no idioma da interface. O modelo fica vazio: não fixamos IDs de modelo.</summary>
    public static ExtensionsDocument CreateDefault(string? providerId) => new()
    {
        Extensions =
        [
            new()
            {
                Name = CoreStrings.ExampleImproveName, Icon = "wand-sparkles", Mode = OutputMode.Suggest, Order = 0, ProviderId = providerId,
                Prompt = CoreStrings.ExampleImprovePrompt,
            },
            new()
            {
                Name = CoreStrings.ExampleFormalName, Icon = "briefcase", Mode = OutputMode.Suggest, Order = 1, ProviderId = providerId,
                Prompt = CoreStrings.ExampleFormalPrompt,
            },
            new()
            {
                Name = CoreStrings.ExampleTranslateName, Icon = "languages", Mode = OutputMode.Replace, Order = 2, ProviderId = providerId,
                Prompt = CoreStrings.ExampleTranslatePrompt,
            },
            new()
            {
                Name = CoreStrings.ExampleRefactorName, Icon = "code", Mode = OutputMode.Suggest, Order = 3, ProviderId = providerId,
                TreatAsCode = true,
                Prompt = CoreStrings.ExampleRefactorPrompt,
            },
        ],
    };
}
