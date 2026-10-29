// Converte os ícones do pacote npm lucide-static (versão fixada abaixo) em path data
// para o controle LucideIcon. Baixa o pacote do registro npm; não precisa de Node.
// Uso (na raiz do repositório): dotnet run tools/gen-icons/gen-lucide.cs
#:property ManagePackageVersionsCentrally=false
#:property NoWarn=$(NoWarn);CA1305
#:property PublishAot=false

using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

const string Version = "1.64.0";

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var root = FindRepoRoot();
var url = $"https://registry.npmjs.org/lucide-static/-/lucide-static-{Version}.tgz";

using var http = new HttpClient();
await using var gzip = new GZipStream(await http.GetStreamAsync(url), CompressionMode.Decompress);
using var tar = new TarReader(gzip);

var icons = new SortedDictionary<string, string>(StringComparer.Ordinal);
string? license = null;
while (await tar.GetNextEntryAsync() is { } entry)
{
    if (entry.DataStream is null)
        continue;
    using var reader = new StreamReader(entry.DataStream);
    if (entry.Name == "package/LICENSE")
        license = await reader.ReadToEndAsync();
    else if (entry.Name.StartsWith("package/icons/", StringComparison.Ordinal) && entry.Name.EndsWith(".svg", StringComparison.Ordinal))
        icons[Path.GetFileNameWithoutExtension(entry.Name)] = ToPathData(XDocument.Parse(await reader.ReadToEndAsync()));
}

if (icons.Count == 0 || license is null)
    throw new InvalidDataException("Pacote lucide-static sem ícones ou sem LICENSE.");

var outDir = Path.Combine(root, "src", "Improve.App", "Assets", "Lucide");
Directory.CreateDirectory(outDir);
var json = JsonSerializer.Serialize(new { version = Version, icons }, new JsonSerializerOptions { WriteIndented = false });
File.WriteAllText(Path.Combine(outDir, "lucide.json"), json, new UTF8Encoding(false));
File.WriteAllText(Path.Combine(root, "licenses", "Lucide-ISC.txt"), license, new UTF8Encoding(false));
Console.WriteLine($"{icons.Count} ícones do lucide-static {Version} gravados em {Path.GetRelativePath(root, outDir)}");

static string ToPathData(XDocument svg)
{
    var parts = new List<string>();
    foreach (var el in svg.Root!.Descendants())
    {
        double A(string name) => double.Parse((string?)el.Attribute(name) ?? "0", CultureInfo.InvariantCulture);
        switch (el.Name.LocalName)
        {
            case "path":
                parts.Add(NormalizePath((string)el.Attribute("d")!));
                break;
            case "circle":
                parts.Add(Ellipse(A("cx"), A("cy"), A("r"), A("r")));
                break;
            case "ellipse":
                parts.Add(Ellipse(A("cx"), A("cy"), A("rx"), A("ry")));
                break;
            case "line":
                parts.Add($"M{N(A("x1"))} {N(A("y1"))}L{N(A("x2"))} {N(A("y2"))}");
                break;
            case "polyline":
            case "polygon":
                var points = ((string)el.Attribute("points")!).Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
                var sb = new StringBuilder($"M{points[0]} {points[1]}");
                for (var i = 2; i + 1 < points.Length; i += 2)
                    sb.Append($"L{points[i]} {points[i + 1]}");
                if (el.Name.LocalName == "polygon")
                    sb.Append('Z');
                parts.Add(sb.ToString());
                break;
            case "rect":
                double rx = A("rx"), ry = el.Attribute("ry") is null ? rx : A("ry");
                parts.Add(Rect(A("x"), A("y"), A("width"), A("height"), el.Attribute("rx") is null ? ry : rx, ry));
                break;
            case "g" or "title" or "desc":
                break;
            default:
                throw new InvalidDataException($"Elemento SVG não suportado: {el.Name.LocalName}");
        }
    }
    return string.Join(" ", parts);
}

static string Ellipse(double cx, double cy, double rx, double ry) =>
    $"M{N(cx - rx)} {N(cy)}A{N(rx)} {N(ry)} 0 1 0 {N(cx + rx)} {N(cy)}A{N(rx)} {N(ry)} 0 1 0 {N(cx - rx)} {N(cy)}Z";

static string Rect(double x, double y, double w, double h, double rx, double ry)
{
    rx = Math.Min(rx, w / 2);
    ry = Math.Min(ry, h / 2);
    if (rx <= 0 || ry <= 0)
        return $"M{N(x)} {N(y)}H{N(x + w)}V{N(y + h)}H{N(x)}Z";
    string Arc(double ex, double ey) => $"A{N(rx)} {N(ry)} 0 0 1 {N(ex)} {N(ey)}";
    return $"M{N(x + rx)} {N(y)}H{N(x + w - rx)}{Arc(x + w, y + ry)}V{N(y + h - ry)}{Arc(x + w - rx, y + h)}" +
           $"H{N(x + rx)}{Arc(x, y + h - ry)}V{N(y + ry)}{Arc(x + rx, y)}Z";
}

// Reescreve o path com separadores explícitos. O SVG minificado do Lucide usa formas
// compactas ("a.5.5 0 0 1", flags de arco coladas como "011") que nem todo parser aceita.
static string NormalizePath(string d)
{
    var sb = new StringBuilder();
    var i = 0;
    char command = ' ';
    while (true)
    {
        SkipSeparators(d, ref i);
        if (i >= d.Length)
            break;
        if (char.IsLetter(d[i]) && d[i] is not ('e' or 'E'))
        {
            command = d[i++];
            // O "m" inicial é absoluto; como os paths são concatenados, ele precisa virar "M".
            sb.Append(sb.Length == 0 && command == 'm' ? 'M' : command);
            if (command is 'z' or 'Z')
                continue;
        }
        else if (command is ' ' or 'z' or 'Z')
        {
            throw new FormatException($"Path inválido: {d}");
        }
        else
        {
            // Repetição implícita do comando anterior (M vira L, m vira l).
            sb.Append(command switch { 'M' => 'L', 'm' => 'l', _ => command });
        }
        var arity = char.ToUpperInvariant(command) switch
        {
            'M' or 'L' or 'T' => 2,
            'H' or 'V' => 1,
            'S' or 'Q' => 4,
            'C' => 6,
            'A' => 7,
            _ => throw new FormatException($"Comando de path não suportado: {command}"),
        };
        for (var p = 0; p < arity; p++)
        {
            SkipSeparators(d, ref i);
            var isFlag = char.ToUpperInvariant(command) == 'A' && p is 3 or 4;
            sb.Append(p == 0 ? "" : " ").Append(isFlag ? d[i++].ToString() : ReadNumber(d, ref i));
        }
        if (command == 'M')
            command = 'L';
        else if (command == 'm')
            command = 'l';
    }
    return sb.ToString();
}

static void SkipSeparators(string d, ref int i)
{
    while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ','))
        i++;
}

static string ReadNumber(string d, ref int i)
{
    var start = i;
    if (i < d.Length && d[i] is '-' or '+')
        i++;
    var seenDot = false;
    while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !seenDot)))
        seenDot |= d[i++] == '.';
    if (i < d.Length && d[i] is 'e' or 'E')
    {
        i++;
        if (d[i] is '-' or '+')
            i++;
        while (i < d.Length && char.IsDigit(d[i]))
            i++;
    }
    if (i == start)
        throw new FormatException($"Número esperado na posição {i}: {d}");
    return d[start..i];
}

static string N(double v) => Math.Round(v, 3).ToString(CultureInfo.InvariantCulture);

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "design", "tokens.json")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
}
