// Gera os ícones do app (PNG 16–1024, .ico, .icns) e os glifos da bandeja a partir de design/icons/.
// Uso (na raiz do repositório): dotnet run tools/gen-icons/gen-app-icon.cs
#:package Svg.Skia@5.2.3
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false

using System.Buffers.Binary;
using SkiaSharp;
using Svg.Skia;

var root = FindRepoRoot();
var iconsDir = Path.Combine(root, "design", "icons");
var assets = Path.Combine(root, "src", "Improve.App", "Assets");

// Ícone do app.
var appDir = Path.Combine(assets, "AppIcon");
Directory.CreateDirectory(appDir);
var app = Path.Combine(iconsDir, "improve-app-icon.svg");
int[] pngSizes = [16, 24, 32, 48, 64, 128, 256, 512, 1024];
int[] icoSizes = [16, 24, 32, 48, 64, 128, 256];
var pngs = pngSizes.ToDictionary(s => s, s => Render(app, s));
foreach (var (size, png) in pngs)
    File.WriteAllBytes(Path.Combine(appDir, $"improve-{size}.png"), png);
File.WriteAllBytes(Path.Combine(appDir, "improve.ico"), Ico([.. icoSizes.Select(s => (s, pngs[s]))]));
File.WriteAllBytes(Path.Combine(appDir, "improve.icns"), Icns(
[
    ("icp4", pngs[16]), ("icp5", pngs[32]), ("icp6", pngs[64]), ("ic07", pngs[128]), ("ic08", pngs[256]),
    ("ic09", pngs[512]), ("ic10", pngs[1024]), ("ic11", pngs[32]), ("ic12", pngs[64]), ("ic13", pngs[256]), ("ic14", pngs[512]),
]));

// Glifos da bandeja: "light" (claro, para barras escuras) e "mono" (escuro, para barras claras e template do macOS).
var trayDir = Path.Combine(assets, "Tray");
Directory.CreateDirectory(trayDir);
string[] variants = ["light", "mono"];
int[] traySizes = [16, 32, 64];
foreach (var variant in variants)
    foreach (var size in traySizes)
        File.WriteAllBytes(Path.Combine(trayDir, $"glyph-{variant}-{size}.png"), Render(Path.Combine(iconsDir, $"improve-glyph-{variant}.svg"), size));

Console.WriteLine($"Ícones gravados em {Path.GetRelativePath(root, appDir)} e {Path.GetRelativePath(root, trayDir)}");

static byte[] Render(string svgPath, int size)
{
    using var svg = new SKSvg();
    var picture = svg.Load(svgPath) ?? throw new InvalidDataException($"SVG inválido: {svgPath}");
    var bounds = picture.CullRect;
    using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);
    canvas.Scale(size / bounds.Width, size / bounds.Height);
    canvas.DrawPicture(picture);
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}

// ICO com imagens PNG embutidas (aceito desde o Windows Vista).
static byte[] Ico((int Size, byte[] Png)[] images)
{
    using var ms = new MemoryStream();
    using var w = new BinaryWriter(ms);
    w.Write((ushort)0);
    w.Write((ushort)1);
    w.Write((ushort)images.Length);
    var offset = 6 + 16 * images.Length;
    foreach (var (size, png) in images)
    {
        w.Write((byte)(size >= 256 ? 0 : size));
        w.Write((byte)(size >= 256 ? 0 : size));
        w.Write((byte)0);
        w.Write((byte)0);
        w.Write((ushort)1);
        w.Write((ushort)32);
        w.Write(png.Length);
        w.Write(offset);
        offset += png.Length;
    }
    foreach (var (_, png) in images)
        w.Write(png);
    return ms.ToArray();
}

// ICNS: cabeçalho "icns" + entradas (tipo, tamanho big-endian, PNG).
static byte[] Icns((string Type, byte[] Png)[] entries)
{
    using var ms = new MemoryStream();
    var total = 8 + entries.Sum(e => 8 + e.Png.Length);
    ms.Write("icns"u8);
    WriteBigEndian(ms, total);
    foreach (var (type, png) in entries)
    {
        ms.Write(System.Text.Encoding.ASCII.GetBytes(type));
        WriteBigEndian(ms, 8 + png.Length);
        ms.Write(png);
    }
    return ms.ToArray();
}

static void WriteBigEndian(Stream s, int value)
{
    Span<byte> buffer = stackalloc byte[4];
    BinaryPrimitives.WriteInt32BigEndian(buffer, value);
    s.Write(buffer);
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "design", "tokens.json")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
}
