using System.Text.RegularExpressions;

namespace Improve.Core.Prompting;

/// <summary>Limpa a resposta da IA para ela poder substituir a seleção original.</summary>
public static partial class ResponseCleaner
{
    public static string Clean(string response, string originalSelection)
    {
        var text = response.Trim();

        if (!originalSelection.Contains("```", StringComparison.Ordinal)
            && WholeFence().Match(text) is { Success: true } fence
            && !fence.Groups["body"].Value.Contains("```", StringComparison.Ordinal))
            text = fence.Groups["body"].Value;

        text = TextTags().Replace(text, "").Trim();

        // Se a seleção terminava em quebra de linha, o resultado também termina, com a mesma sequência.
        return text + TrailingLineBreaks(originalSelection);
    }

    private static string TrailingLineBreaks(string text)
    {
        var end = text.Length;
        while (end > 0 && text[end - 1] is '\n' or '\r')
            end--;
        return text[end..];
    }

    [GeneratedRegex(@"\A```[^\n]*\n(?<body>.*?)\n?```\z", RegexOptions.Singleline)]
    private static partial Regex WholeFence();

    [GeneratedRegex(@"</?texto>")]
    private static partial Regex TextTags();
}
