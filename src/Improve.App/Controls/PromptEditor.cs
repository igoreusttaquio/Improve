using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace Improve.App.Controls;

/// <summary>
/// Campo de prompt em fonte mono com realce de <c>{{variáveis}}</c>. O TextBox fica com o texto transparente
/// e um TextBlock com a mesma fonte, quebra e margem desenha o texto colorido por cima.
/// </summary>
public sealed partial class PromptEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PromptEditor, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    private readonly TextBox _input;
    private readonly TextBlock _overlay;

    public PromptEditor()
    {
        _input = new TextBox { MinHeight = 120, Foreground = Brushes.Transparent };
        _input.Classes.AddRange(["code", "multiline"]);
        _overlay = new TextBlock
        {
            IsHitTestVisible = false,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _overlay.Classes.Add("code");
        // Mesma posição do texto dentro do TextBox: borda (1) + padding da classe multiline (12,8).
        _overlay.Margin = new Thickness(13, 9, 13, 9);

        _input.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Text = _input.Text;
                UpdateOverlay();
            }
        };
        ActualThemeVariantChanged += (_, _) => UpdateOverlay();
        Content = new Panel { Children = { _input, _overlay } };
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty && _input.Text != Text)
            _input.Text = Text;

    }

    private void UpdateOverlay()
    {
        var text = _input.Text ?? "";
        var inlines = new InlineCollection();
        var last = 0;
        foreach (Match match in Variable().Matches(text))
        {
            if (match.Index > last)
                inlines.Add(new Run(text[last..match.Index]) { Foreground = Resource("InkBrush") });
            // Só a cor muda: peso e tamanho iguais para não desalinhar do texto real.
            inlines.Add(new Run(match.Value) { Foreground = Resource("AccentInkBrush"), Background = Resource("AccentSoftBrush") });
            last = match.Index + match.Length;
        }
        if (last < text.Length)
            inlines.Add(new Run(text[last..]) { Foreground = Resource("InkBrush") });
        _overlay.Inlines = inlines;
    }

    private IBrush? Resource(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    [GeneratedRegex(@"\{\{(selection|app|clipboard)\}\}")]
    private static partial Regex Variable();
}
