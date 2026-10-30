using System.Text.Json;
using System.Text.RegularExpressions;

namespace Improve.Core.Prompting;

/// <summary>Linguagem para o realce de código; o id é o mesmo das gramáticas TextMate (VS Code).</summary>
public sealed record CodeLanguage(string Id, string Name);

/// <summary>Linguagens oferecidas no editor de extensão e detecção automática pela seleção.</summary>
public static partial class CodeLanguages
{
    public static IReadOnlyList<CodeLanguage> All { get; } =
    [
        new("csharp", "C#"),
        new("javascript", "JavaScript"),
        new("typescript", "TypeScript"),
        new("python", "Python"),
        new("java", "Java"),
        new("go", "Go"),
        new("rust", "Rust"),
        new("cpp", "C/C++"),
        new("php", "PHP"),
        new("ruby", "Ruby"),
        new("swift", "Swift"),
        new("sql", "SQL"),
        new("json", "JSON"),
        new("yaml", "YAML"),
        new("xml", "XML"),
        new("html", "HTML"),
        new("css", "CSS"),
        new("shellscript", "Bash"),
        new("powershell", "PowerShell"),
        new("markdown", "Markdown"),
    ];

    // Padrões típicos de cada linguagem; ganha a que somar mais acertos.
    private static readonly (string Id, Regex Pattern, int Weight)[] Signals =
    [
        ("csharp", CSharp(), 3),
        ("typescript", TypeScript(), 3),
        ("javascript", JavaScript(), 2),
        ("python", Python(), 3),
        ("java", Java(), 3),
        ("go", Go(), 3),
        ("rust", Rust(), 3),
        ("cpp", Cpp(), 3),
        ("php", Php(), 4),
        ("ruby", Ruby(), 2),
        ("swift", Swift(), 3),
        ("sql", Sql(), 3),
        ("css", Css(), 3),
        ("shellscript", Shell(), 3),
        ("powershell", PowerShell(), 3),
    ];

    /// <summary>Detecta a linguagem do texto, ou <c>null</c> se não houver sinal suficiente.</summary>
    public static string? Detect(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return null;

        // Formatos de dados, por estrutura.
        if (trimmed[0] is '{' or '[' && IsJson(trimmed))
            return "json";
        if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            return "xml";
        if (trimmed.StartsWith("<?php", StringComparison.OrdinalIgnoreCase))
            return "php";
        if (trimmed[0] == '<' && Html().IsMatch(trimmed))
            return "html";
        if (trimmed[0] == '<' && trimmed.EndsWith('>'))
            return "xml";

        var best = Signals
            .Select(s => (s.Id, Score: s.Pattern.Count(text) * s.Weight))
            .OrderByDescending(s => s.Score)
            .First();
        if (best.Score >= 3)
            return best.Id;
        return Yaml().Count(text) >= 2 && !text.Contains('{', StringComparison.Ordinal) ? "yaml" : null;
    }

    private static bool IsJson(string text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"\b(using System|namespace \w|public (sealed |static |partial )*(class|record|interface)|\{ get; (set|init); \}|async Task|var \w+ = new\b|string\? )")]
    private static partial Regex CSharp();

    [GeneratedRegex(@"(\binterface \w+ \{|: (string|number|boolean)\b|\btype \w+ = |\bexport (default |const |function |interface )|\bimport .+ from ['""])")]
    private static partial Regex TypeScript();

    [GeneratedRegex(@"(\bfunction\s*\w*\s*\(|\bconst \w+ = |\blet \w+ = |console\.log|=>\s*\{|\brequire\(|document\.)")]
    private static partial Regex JavaScript();

    [GeneratedRegex(@"(^\s*def \w+\(|^\s*import \w+$|^\s*from \w+ import|\bself\.|\belif\b|print\(|^\s*class \w+(\(.*\))?:)", RegexOptions.Multiline)]
    private static partial Regex Python();

    [GeneratedRegex(@"(public static void main|System\.out\.print|import java\.|@Override|\bprivate final\b)")]
    private static partial Regex Java();

    [GeneratedRegex(@"(^package \w+|\bfunc \w*\(|:= |\bfmt\.|\bgo func)", RegexOptions.Multiline)]
    private static partial Regex Go();

    [GeneratedRegex(@"(\bfn \w+\(|\blet mut\b|\bimpl\b|println!|\bpub fn\b|&mut\b|::new\()")]
    private static partial Regex Rust();

    [GeneratedRegex(@"(#include\s*<|\bstd::|int main\s*\(|\bcout\s*<<|->\w+\(|\bnullptr\b)")]
    private static partial Regex Cpp();

    [GeneratedRegex(@"(\$\w+\s*=|\becho\b.*;|->\w+\(|\bfunction \w+\(\$)")]
    private static partial Regex Php();

    [GeneratedRegex(@"(^\s*def \w+[^(:]*$|^\s*end$|\bputs\b|\.each do\b|\battr_accessor\b)", RegexOptions.Multiline)]
    private static partial Regex Ruby();

    [GeneratedRegex(@"(\bfunc \w+\(.*\) -> |\bguard let\b|\bif let\b|\bvar \w+: \w+|\bstruct \w+: )")]
    private static partial Regex Swift();

    [GeneratedRegex(@"\b(SELECT\b.+\bFROM|INSERT INTO|UPDATE \w+ SET|DELETE FROM|CREATE TABLE|ALTER TABLE|INNER JOIN|LEFT JOIN|WHERE \w+ =|GROUP BY|ORDER BY)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Sql();

    [GeneratedRegex(@"(^\s*[.#]?[\w-]+(\s*[,>+~]\s*[.#]?[\w-]+)*\s*\{|^\s*[\w-]+\s*:\s*[^;{]+;\s*$|@media\b)", RegexOptions.Multiline)]
    private static partial Regex Css();

    [GeneratedRegex(@"(^#!/(usr/)?bin/(env )?(ba)?sh|^\s*(if \[|fi$|then$|done$)|\bsudo\b|\becho \$|\$\(\w)", RegexOptions.Multiline)]
    private static partial Regex Shell();

    [GeneratedRegex(@"(\b(Get|Set|New|Remove|Write|Invoke)-\w+|\$\w+ = |-(eq|ne|gt|lt)\b|\[\w+\]\$|\bparam\s*\()", RegexOptions.IgnoreCase)]
    private static partial Regex PowerShell();

    [GeneratedRegex(@"<(!DOCTYPE html|html|head|body|div|span|p|a|ul|li|section|button|form|input|img)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Html();

    [GeneratedRegex(@"^\s*-?\s*[\w-]+:(\s+\S.*)?$", RegexOptions.Multiline)]
    private static partial Regex Yaml();
}
