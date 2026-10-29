using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Improve.App.Controls;

/// <summary>
/// Editor de prompt (AvaloniaEdit) em fonte mono, com realce do Markdown usado em prompts
/// (títulos, listas, negrito, itálico, código, citações) e das <c>{{variáveis}}</c>.
/// </summary>
public sealed partial class PromptEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PromptEditor, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    private readonly TextEditor _editor;

    public PromptEditor()
    {
        _editor = new TextEditor
        {
            WordWrap = true,
            ShowLineNumbers = false,
            MinHeight = 120,
            MaxHeight = 320,
            Padding = new Thickness(12, 8),
            Background = Brushes.Transparent,
        };
        _editor.Classes.Add("prompt");
        _editor.TextArea.TextView.LineTransformers.Add(new PromptColorizer(this));
        _editor.Document.TextChanged += (_, _) => Text = _editor.Document.Text;
        _editor.TextArea.GotFocus += (_, _) => PseudoClasses.Set(":editing", true);
        _editor.TextArea.LostFocus += (_, _) => PseudoClasses.Set(":editing", false);
        ActualThemeVariantChanged += (_, _) =>
        {
            ApplyThemeBrushes();
            _editor.TextArea.TextView.Redraw();
        };

        Content = new Border { Classes = { "prompt-frame" }, Child = _editor };
        ApplyThemeBrushes();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty && _editor.Document.Text != (Text ?? ""))
            _editor.Document.Text = Text ?? "";
    }

    internal IBrush? Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    private void ApplyThemeBrushes()
    {
        _editor.Foreground = Brush("InkBrush");
        _editor.TextArea.SelectionBrush = Brush("AccentSoftBrush");
        _editor.TextArea.SelectionForeground = null;
        _editor.TextArea.Caret.CaretBrush = Brush("InkBrush");
    }

    /// <summary>Colore cada linha com expressões regulares; só cor, peso e estilo, sem mudar o texto.</summary>
    private sealed partial class PromptColorizer(PromptEditor owner) : DocumentColorizingTransformer
    {
        protected override void ColorizeLine(DocumentLine line)
        {
            var text = CurrentContext.Document.GetText(line);
            var start = line.Offset;

            if (Heading().Match(text) is { Success: true } heading)
            {
                Apply(start, start + text.Length, owner.Brush("InkBrush"), weight: FontWeight.SemiBold);
                Apply(start + heading.Groups["mark"].Index, start + heading.Groups["mark"].Index + heading.Groups["mark"].Length, owner.Brush("InkMutedBrush"));
            }
            else if (Quote().IsMatch(text))
            {
                Apply(start, start + text.Length, owner.Brush("InkMutedBrush"), style: FontStyle.Italic);
            }

            if (ListMarker().Match(text) is { Success: true } marker)
                Apply(start + marker.Groups["mark"].Index, start + marker.Groups["mark"].Index + marker.Groups["mark"].Length, owner.Brush("AccentInkBrush"));

            foreach (Match m in Bold().Matches(text))
                Apply(start + m.Index, start + m.Index + m.Length, null, weight: FontWeight.SemiBold);
            foreach (Match m in Italic().Matches(text))
                Apply(start + m.Index, start + m.Index + m.Length, null, style: FontStyle.Italic);
            foreach (Match m in InlineCode().Matches(text))
                Apply(start + m.Index, start + m.Index + m.Length, owner.Brush("InkBrush"), background: owner.Brush("Surface300Brush"));
            foreach (Match m in Variable().Matches(text))
                Apply(start + m.Index, start + m.Index + m.Length, owner.Brush("AccentInkBrush"), background: owner.Brush("AccentSoftBrush"));
        }

        private void Apply(int from, int to, IBrush? foreground, IBrush? background = null, FontWeight? weight = null, FontStyle? style = null)
        {
            if (to <= from)
                return;
            ChangeLinePart(from, to, element =>
            {
                if (foreground is not null)
                    element.TextRunProperties.SetForegroundBrush(foreground);
                if (background is not null)
                    element.TextRunProperties.SetBackgroundBrush(background);
                if (weight is not null || style is not null)
                {
                    var current = element.TextRunProperties.Typeface;
                    element.TextRunProperties.SetTypeface(new Typeface(current.FontFamily, style ?? current.Style, weight ?? current.Weight));
                }
            });
        }

        [GeneratedRegex(@"^(?<mark>#{1,6})\s")]
        private static partial Regex Heading();

        [GeneratedRegex(@"^\s*>")]
        private static partial Regex Quote();

        [GeneratedRegex(@"^\s*(?<mark>[-*+]|\d+[.)])\s")]
        private static partial Regex ListMarker();

        [GeneratedRegex(@"\*\*[^*\n]+\*\*|__[^_\n]+__")]
        private static partial Regex Bold();

        [GeneratedRegex(@"(?<![*\w])\*[^*\s][^*\n]*\*(?!\*)|(?<![_\w])_[^_\s][^_\n]*_(?!_)")]
        private static partial Regex Italic();

        [GeneratedRegex(@"`[^`\n]+`")]
        private static partial Regex InlineCode();

        [GeneratedRegex(@"\{\{(selection|app|clipboard)\}\}")]
        private static partial Regex Variable();
    }
}
