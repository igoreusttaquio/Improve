// Gera os recursos Avalonia a partir de design/tokens.json.
// Uso (na raiz do repositório):
//   dotnet run tools/gen-tokens/gen-tokens.cs            grava os arquivos
//   dotnet run tools/gen-tokens/gen-tokens.cs -- --check falha se os arquivos estiverem desatualizados
#:property ManagePackageVersionsCentrally=false
#:property NoWarn=$(NoWarn);CA1305

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Toda formatação do script é invariante (por isso o CA1305 fica desligado acima).
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var check = args.Contains("--check");
var root = FindRepoRoot();
using var tokens = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "design", "tokens.json")));
var themesDir = Path.Combine(root, "src", "Improve.App", "Themes");

var outputs = new Dictionary<string, string>
{
    ["Tokens.Dark.axaml"] = ThemeDictionary(tokens.RootElement, "dark"),
    ["Tokens.Light.axaml"] = ThemeDictionary(tokens.RootElement, "light"),
    ["Tokens.axaml"] = SharedDictionary(tokens.RootElement),
    ["Typography.axaml"] = Typography(tokens.RootElement),
};

var stale = new List<string>();
foreach (var (file, content) in outputs)
{
    var path = Path.Combine(themesDir, file);
    var current = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : null;
    if (current == content)
        continue;
    if (check)
    {
        stale.Add(file);
        continue;
    }
    Directory.CreateDirectory(themesDir);
    File.WriteAllText(path, content, new UTF8Encoding(false));
    Console.WriteLine($"gravado: {Path.GetRelativePath(root, path)}");
}

if (stale.Count > 0)
{
    Console.Error.WriteLine($"Tokens desatualizados ({string.Join(", ", stale)}). Rode: dotnet run tools/gen-tokens/gen-tokens.cs");
    return 1;
}
return 0;

static string ThemeDictionary(JsonElement root, string theme)
{
    var sb = Header("ResourceDictionary");
    foreach (var token in root.GetProperty("color").GetProperty("tokens").EnumerateArray())
    {
        var name = token.GetProperty("name").GetString()!;
        var color = ToAvaloniaColor(token.GetProperty("value").GetProperty(theme).GetString()!);
        sb.Append($"  <SolidColorBrush x:Key=\"{Pascal(name)}Brush\" Color=\"{color}\" />\n");
    }
    foreach (var token in root.GetProperty("shadow").GetProperty("tokens").EnumerateArray())
    {
        var name = token.GetProperty("name").GetString()!;
        var value = token.GetProperty("value").GetProperty(theme).GetString()!;
        sb.Append($"  <BoxShadows x:Key=\"{Pascal(name)}\">{ToBoxShadows(value)}</BoxShadows>\n");
    }
    return Footer(sb, "ResourceDictionary");
}

static string SharedDictionary(JsonElement root)
{
    var sb = Header("ResourceDictionary");
    foreach (var token in root.GetProperty("spacing").GetProperty("tokens").EnumerateArray())
    {
        var key = Pascal(token.GetProperty("name").GetString()!);
        var px = Px(token.GetProperty("value").GetString()!);
        sb.Append($"  <x:Double x:Key=\"{key}\">{px}</x:Double>\n");
        sb.Append($"  <Thickness x:Key=\"{key}Thickness\">{px}</Thickness>\n");
    }
    foreach (var token in root.GetProperty("radius").GetProperty("tokens").EnumerateArray())
    {
        var key = Pascal(token.GetProperty("name").GetString()!);
        sb.Append($"  <CornerRadius x:Key=\"{key}\">{Px(token.GetProperty("value").GetString()!)}</CornerRadius>\n");
    }
    foreach (var family in root.GetProperty("type").GetProperty("families").EnumerateObject())
        sb.Append($"  <FontFamily x:Key=\"Font{Pascal(family.Name)}\">{ToFontFamily(family.Value.GetString()!)}</FontFamily>\n");
    return Footer(sb, "ResourceDictionary");
}

static string Typography(JsonElement root)
{
    var sb = Header("Styles");
    foreach (var group in root.GetProperty("type").GetProperty("groups").EnumerateArray())
    {
        var family = Pascal(group.GetProperty("family").GetString()!);
        foreach (var style in group.GetProperty("styles").EnumerateArray())
        {
            var size = Px(style.GetProperty("fontSize").GetString()!);
            sb.Append($"  <Style Selector=\"TextBlock.{style.GetProperty("name").GetString()}\">\n");
            sb.Append($"    <Setter Property=\"FontFamily\" Value=\"{{StaticResource Font{family}}}\" />\n");
            sb.Append($"    <Setter Property=\"FontSize\" Value=\"{size}\" />\n");
            sb.Append($"    <Setter Property=\"LineHeight\" Value=\"{Px(style.GetProperty("lineHeight").GetString()!)}\" />\n");
            sb.Append($"    <Setter Property=\"FontWeight\" Value=\"{ToFontWeight(style.GetProperty("fontWeight").GetInt32())}\" />\n");
            if (style.TryGetProperty("letterSpacing", out var spacing))
            {
                var em = double.Parse(spacing.GetString()!.Replace("em", ""), CultureInfo.InvariantCulture);
                sb.Append($"    <Setter Property=\"LetterSpacing\" Value=\"{Num(em * double.Parse(size, CultureInfo.InvariantCulture))}\" />\n");
            }
            sb.Append("  </Style>\n");
        }
    }
    return Footer(sb, "Styles");
}

static StringBuilder Header(string element)
{
    var sb = new StringBuilder();
    sb.Append("<!-- Gerado por tools/gen-tokens a partir de design/tokens.json. Não edite à mão. -->\n");
    sb.Append($"<{element} xmlns=\"https://github.com/avaloniaui\"\n");
    sb.Append($"{new string(' ', element.Length + 2)}xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n");
    return sb;
}

static string Footer(StringBuilder sb, string element) => sb.Append($"</{element}>\n").ToString();

// "surface-100" → "Surface100", "on-accent" → "OnAccent".
static string Pascal(string name) =>
    string.Concat(name.Split('-').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

static string Px(string value) => value.EndsWith("px", StringComparison.Ordinal) ? value[..^2] : value;

static string Num(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

// "#RRGGBB" fica igual; "rgba(r, g, b, a)" vira "#AARRGGBB".
static string ToAvaloniaColor(string css)
{
    css = css.Trim();
    if (css.StartsWith('#'))
        return css.ToUpperInvariant();
    var m = Regex.Match(css, @"^rgba\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*([\d.]+)\s*\)$");
    if (!m.Success)
        throw new FormatException($"Cor não suportada: {css}");
    var a = (int)Math.Round(double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) * 255);
    return $"#{a:X2}{int.Parse(m.Groups[1].Value):X2}{int.Parse(m.Groups[2].Value):X2}{int.Parse(m.Groups[3].Value):X2}";
}

// CSS "x y blur [spread] color, ..." → Avalonia "x y blur spread color, ...".
static string ToBoxShadows(string css)
{
    var shadows = Regex.Split(css, @",(?![^(]*\))").Select(s =>
    {
        var m = Regex.Match(s.Trim(), @"^((?:-?[\d.]+(?:px)?\s+){2,4})(.+)$");
        if (!m.Success)
            throw new FormatException($"Sombra não suportada: {css}");
        var lengths = m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Px).ToList();
        while (lengths.Count < 4)
            lengths.Add("0");
        return $"{string.Join(' ', lengths)} {ToAvaloniaColor(m.Groups[2].Value)}";
    });
    return string.Join(", ", shadows);
}

// Inter e JetBrains Mono vêm embutidas (coleção "fonts:Improve"); nomes genéricos do CSS são descartados.
static string ToFontFamily(string css)
{
    string[] embedded = ["Inter", "JetBrains Mono"];
    string[] generic = ["system-ui", "sans-serif", "ui-monospace", "monospace", "serif"];
    var names = css.Split(',')
        .Select(n => n.Trim().Trim('"'))
        .Where(n => !generic.Contains(n))
        .Select(n => embedded.Contains(n) ? $"fonts:Improve#{n}" : n);
    return string.Join(", ", names);
}

// Pesos sem nome (ex.: 650) são aceitos como número pelo Avalonia.
static string ToFontWeight(int weight) => weight switch
{
    400 => "Normal",
    500 => "Medium",
    600 => "SemiBold",
    700 => "Bold",
    _ => weight.ToString(CultureInfo.InvariantCulture),
};

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "design", "tokens.json")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new DirectoryNotFoundException("design/tokens.json não encontrado.");
}
