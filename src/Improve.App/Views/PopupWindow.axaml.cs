using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Improve.App.Controls;
using Improve.App.ViewModels;

namespace Improve.App.Views;

/// <summary>
/// Janela flutuante do popup e do painel de sugestão. Fica criada e oculta; aparece 12 px abaixo e à
/// direita do cursor, ajustada para caber no monitor do cursor.
/// </summary>
public partial class PopupWindow : Window
{
    private const double CursorOffset = 12;

    /// <summary>Largura do painel de texto, e a mínima do painel de código.</summary>
    private const double PanelWidth = 420;

    // Margens do CodeView dentro do painel (estilo panel-code).
    private const double CodeMargin = 24;

    private double _maxPanelWidth = 1200;
    private double _maxCodeHeight = 400;

    private PixelPoint _anchor;

    /// <summary>O usuário arrastou a janela nesta exibição: não volta para junto do cursor ao mudar de tamanho.</summary>
    private bool _movedByUser;

    public PopupWindow() => InitializeComponent();

    public PopupWindow(PopupViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => FocusLost?.Invoke(this, EventArgs.Empty);
        // Ao trocar de estado o tamanho muda: reposiciona mantendo o mesmo ponto de ancoragem.
        SizeChanged += (_, _) => Place();
        // Arrastar pelas áreas livres do cartão (botões, campos, linhas e o texto tratam o clique antes).
        Card.PointerPressed += OnCardPressed;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && !IsVisible)
            {
                _movedByUser = false;
                ResetSize();
            }
        };
        CodeBody.PropertyChanged += (_, e) =>
        {
            if (e.Property == CodeView.PreferredSizeProperty || e.Property == IsVisibleProperty)
                FitToCode();
        };
    }

    /// <summary>A janela perdeu o foco (o <c>FlowController</c> decide se fecha).</summary>
    public event EventHandler? FocusLost;

    private PopupViewModel ViewModel => (PopupViewModel)DataContext!;

    /// <param name="anchor">Posição do cursor em pixels físicos.</param>
    /// <param name="activate">Toma o foco do teclado (o aviso rápido não toma).</param>
    public void ShowAt(PixelPoint anchor, bool activate)
    {
        _anchor = anchor;
        // O código cresce até 120 colunas, mas sem passar do monitor.
        if ((Screens.ScreenFromPoint(anchor) ?? Screens.Primary) is { } screen)
        {
            var margin = Card.Margin;
            _maxPanelWidth = Math.Max(PanelWidth, screen.WorkingArea.Width / screen.Scaling - margin.Left - margin.Right - 2 * CursorOffset);
            _maxCodeHeight = Math.Max(160, screen.WorkingArea.Height / screen.Scaling * 0.5);
        }
        FitToCode();
        Card.Measure(Size.Infinity);
        Place();
        ShowActivated = activate;
        if (!IsVisible)
            Show();
        Card.Classes.Remove("opening");
        Card.Classes.Add("opening");
        if (activate)
            FocusContent();
    }

    /// <summary>
    /// Mostra a janela uma vez, invisível e sem foco, e esconde: a primeira exibição de verdade não paga
    /// o custo de criar a janela nativa, aplicar estilos e carregar fontes.
    /// </summary>
    public void WarmUp(Action done)
    {
        Opacity = 0;
        ShowActivated = false;
        Position = new PixelPoint(-32000, -32000);
        Show();
        Dispatcher.UIThread.Post(() =>
        {
            Hide();
            Opacity = 1;
            done();
        }, DispatcherPriority.Background);
    }

    /// <summary>Foco inicial de cada estado: a busca no popup; o próprio cartão no painel (↵ aplica).</summary>
    public void FocusContent()
    {
        Activate();
        if (ViewModel.IsActions)
            SearchBox.Focus();
        else
            Card.Focus();
    }

    // O painel de código acompanha o conteúdo, para mais ou para menos, com transição (a janela segue
    // pelo SizeToContent e Place mantém a ancoragem). Texto comum continua com a largura fixa.
    private void FitToCode()
    {
        if (!CodeBody.IsVisible)
        {
            SuggestionPanel.Width = PanelWidth;
            return;
        }
        var preferred = CodeBody.PreferredSize;
        SuggestionPanel.Width = Math.Clamp(preferred.Width + CodeMargin, PanelWidth, _maxPanelWidth);
        CodeBody.Height = Math.Min(preferred.Height, _maxCodeHeight);
        CodeBody.ScrollsVertically = preferred.Height > _maxCodeHeight;
    }

    // Oculta, volta ao tamanho inicial sem animar: a próxima exibição não começa encolhendo.
    private void ResetSize()
    {
        var panelTransitions = SuggestionPanel.Transitions;
        var codeTransitions = CodeBody.Transitions;
        SuggestionPanel.Transitions = null;
        CodeBody.Transitions = null;
        SuggestionPanel.Width = PanelWidth;
        CodeBody.Height = double.NaN;
        SuggestionPanel.Transitions = panelTransitions;
        CodeBody.Transitions = codeTransitions;
    }

    private void Place()
    {
        var margin = Card.Margin;
        var reference = _movedByUser ? Position : _anchor;
        if ((Screens.ScreenFromPoint(reference) ?? Screens.Primary) is not { } screen)
            return;
        var scale = screen.Scaling;
        var area = screen.WorkingArea;
        var size = IsVisible ? Bounds.Size : Card.DesiredSize;
        var cardWidth = (int)Math.Ceiling((size.Width - margin.Left - margin.Right) * scale);
        var cardHeight = (int)Math.Ceiling((size.Height - margin.Top - margin.Bottom) * scale);
        var offset = (int)(CursorOffset * scale);

        int left, top;
        if (_movedByUser)
        {
            // Arrastado: fica onde o usuário deixou, só ajustado para caber na tela.
            left = Position.X + (int)(margin.Left * scale);
            top = Position.Y + (int)(margin.Top * scale);
        }
        else
        {
            // O cartão (não a janela, que tem a margem da sombra) fica abaixo/à direita do cursor; vira se não couber.
            left = _anchor.X + offset;
            if (left + cardWidth > area.Right)
                left = _anchor.X - offset - cardWidth;
            top = _anchor.Y + offset;
            if (top + cardHeight > area.Bottom)
                top = _anchor.Y - offset - cardHeight;
        }
        left = Math.Clamp(left, area.X, Math.Max(area.X, area.Right - cardWidth));
        top = Math.Clamp(top, area.Y, Math.Max(area.Y, area.Bottom - cardHeight));

        Position = new PixelPoint(left - (int)(margin.Left * scale), top - (int)(margin.Top * scale));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (e.Key == Key.Escape)
        {
            vm.CloseCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (vm.IsActions)
        {
            switch (e.Key)
            {
                case Key.Up or Key.Down:
                    vm.MoveSelection(e.Key == Key.Up ? -1 : 1);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    vm.RunCommand.Execute(null);
                    e.Handled = true;
                    break;
                // Com algo digitado na busca, os números são texto (podem fazer parte do prompt livre).
                case >= Key.D1 and <= Key.D9 or >= Key.NumPad1 and <= Key.NumPad9 when e.KeyModifiers == KeyModifiers.None && vm.Search.Length == 0:
                    var number = e.Key >= Key.NumPad1 ? e.Key - Key.NumPad1 + 1 : e.Key - Key.D1 + 1;
                    if (vm.ByNumber(number) is { } item)
                        vm.RunCommand.Execute(item);
                    e.Handled = true;
                    break;
            }
        }
        else if (vm.IsSuggestion && e.Key == Key.Enter)
        {
            // No campo Refinar, ↵ refina; fora dele, ↵ aplica.
            if (RefineBox.IsKeyboardFocusWithin)
                vm.RefineCommand.Execute(null);
            else
                vm.ApplyCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: PopupItem item })
        {
            e.Handled = true;
            ViewModel.RunCommand.Execute(item);
        }
    }

    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        _movedByUser = true;
        BeginMoveDrag(e);
    }
}
