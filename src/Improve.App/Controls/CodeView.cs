using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace Improve.App.Controls;

/// <summary>
/// Código somente-leitura com realce de sintaxe (gramáticas TextMate do VS Code). As cores seguem o tema
/// do app (Dark+ / Light+); o fundo fica transparente para usar o da superfície onde está.
/// </summary>
public sealed class CodeView : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CodeView, string?>(nameof(Text));

    /// <summary>Id da linguagem TextMate (ex.: <c>csharp</c>); <c>null</c> mostra sem cores.</summary>
    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<CodeView, string?>(nameof(Language));

    private readonly TextEditor _editor;
    private readonly RegistryOptions _registry;
    private readonly TextMate.Installation _textMate;

    public CodeView()
    {
        _editor = new TextEditor
        {
            IsReadOnly = true,
            ShowLineNumbers = false,
            WordWrap = false,
            Background = Brushes.Transparent,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _editor.Classes.Add("code-view");
        _editor.TextArea.Caret.CaretBrush = Brushes.Transparent;

        _registry = new RegistryOptions(ThemeFor(ActualThemeVariant));
        _textMate = _editor.InstallTextMate(_registry);
        ActualThemeVariantChanged += (_, _) => ApplyTheme();
        Content = _editor;
        ApplyTheme();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
            UpdateText(Text ?? "");
        else if (change.Property == LanguageProperty)
            _textMate.SetGrammar(Language is { } id ? _registry.GetScopeByLanguageId(id) : null);
    }

    // No streaming o texto só cresce: acrescenta o pedaço novo em vez de recolorir o documento inteiro.
    private void UpdateText(string text)
    {
        var document = _editor.Document;
        if (text.Length >= document.TextLength && text.StartsWith(document.Text, StringComparison.Ordinal))
            document.Insert(document.TextLength, text[document.TextLength..]);
        else
            document.Text = text;
    }

    private void ApplyTheme()
    {
        // Um registro criado já com o tema resolve os temas que ele inclui (o Light+ estende o Light).
        _textMate.SetTheme(new RegistryOptions(ThemeFor(ActualThemeVariant)).GetDefaultTheme());
        // O tema TextMate traz fundo e seleção próprios; usamos os da marca.
        _editor.Background = Brushes.Transparent;
        _editor.Foreground = this.TryFindResource("InkBrush", ActualThemeVariant, out var ink) ? ink as IBrush : null;
        _editor.TextArea.SelectionBrush = this.TryFindResource("AccentSoftBrush", ActualThemeVariant, out var soft) ? soft as IBrush : null;
    }

    private static ThemeName ThemeFor(ThemeVariant variant) => variant == ThemeVariant.Light ? ThemeName.LightPlus : ThemeName.DarkPlus;
}
