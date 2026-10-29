using System.Collections.Concurrent;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Platform;

namespace Improve.App.Controls;

/// <summary>
/// Ícone Lucide pelo nome (ex.: <c>wand-sparkles</c>). Desenha o traço com espessura 1.75 no
/// espaço 24×24 do Lucide, escalado para <see cref="Size"/>, na cor herdada de <c>Foreground</c>.
/// </summary>
public sealed class LucideIcon : Control
{
    private const double ViewBox = 24;
    private const double StrokeThickness = 1.75;

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<LucideIcon, string?>(nameof(Icon));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<LucideIcon, double>(nameof(Size), 16);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<LucideIcon>();

    private static readonly Lazy<IReadOnlyDictionary<string, string>> PathData = new(LoadPathData);
    private static readonly ConcurrentDictionary<string, Geometry?> Geometries = new();

    static LucideIcon()
    {
        AffectsRender<LucideIcon>(IconProperty, SizeProperty, ForegroundProperty);
        AffectsMeasure<LucideIcon>(SizeProperty);
    }

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Nomes de todos os ícones disponíveis, para o seletor de ícones.</summary>
    public static IEnumerable<string> AvailableNames => PathData.Value.Keys;

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Icon is not { } name || Foreground is not { } brush || GetGeometry(name) is not { } geometry)
            return;

        var scale = Size / ViewBox;
        var pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
            context.DrawGeometry(null, pen, geometry);
    }

    private static Geometry? GetGeometry(string name) =>
        Geometries.GetOrAdd(name, n => PathData.Value.TryGetValue(n, out var data) ? StreamGeometry.Parse(data) : null);

    private static Dictionary<string, string> LoadPathData()
    {
        using var stream = AssetLoader.Open(new Uri("avares://improve/Assets/Lucide/lucide.json"));
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.GetProperty("icons").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
    }
}
