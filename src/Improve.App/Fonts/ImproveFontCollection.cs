using Avalonia.Media.Fonts;

namespace Improve.App.Fonts;

/// <summary>Inter e JetBrains Mono embutidas, referenciadas como <c>fonts:Improve#Inter</c>.</summary>
public sealed class ImproveFontCollection : EmbeddedFontCollection
{
    public ImproveFontCollection()
        : base(new Uri("fonts:Improve", UriKind.Absolute), new Uri("avares://improve/Assets/Fonts", UriKind.Absolute))
    {
    }
}
