using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Improve.App.ViewModels;

namespace Improve.App.Views;

/// <summary>
/// Janela flutuante do popup e do painel de sugestão. Fica criada e oculta; aparece 12 px abaixo e à
/// direita do cursor, ajustada para caber no monitor do cursor.
/// </summary>
public partial class PopupWindow : Window
{
    private const double CursorOffset = 12;

    private PixelPoint _anchor;

    public PopupWindow() => InitializeComponent();

    public PopupWindow(PopupViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => FocusLost?.Invoke(this, EventArgs.Empty);
        // Ao trocar de estado o tamanho muda: reposiciona mantendo o mesmo ponto de ancoragem.
        SizeChanged += (_, _) => Place();
    }

    /// <summary>A janela perdeu o foco (o <c>FlowController</c> decide se fecha).</summary>
    public event EventHandler? FocusLost;

    private PopupViewModel ViewModel => (PopupViewModel)DataContext!;

    /// <param name="anchor">Posição do cursor em pixels físicos.</param>
    /// <param name="activate">Toma o foco do teclado (o aviso rápido não toma).</param>
    public void ShowAt(PixelPoint anchor, bool activate)
    {
        _anchor = anchor;
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

    private void Place()
    {
        if ((Screens.ScreenFromPoint(_anchor) ?? Screens.Primary) is not { } screen)
            return;
        var scale = screen.Scaling;
        var area = screen.WorkingArea;
        var margin = Card.Margin;
        var size = IsVisible ? Bounds.Size : Card.DesiredSize;
        var cardWidth = (int)Math.Ceiling((size.Width - margin.Left - margin.Right) * scale);
        var cardHeight = (int)Math.Ceiling((size.Height - margin.Top - margin.Bottom) * scale);
        var offset = (int)(CursorOffset * scale);

        // O cartão (não a janela, que tem a margem da sombra) fica abaixo/à direita do cursor; vira se não couber.
        var left = _anchor.X + offset;
        if (left + cardWidth > area.Right)
            left = _anchor.X - offset - cardWidth;
        var top = _anchor.Y + offset;
        if (top + cardHeight > area.Bottom)
            top = _anchor.Y - offset - cardHeight;
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
                case >= Key.D1 and <= Key.D9 or >= Key.NumPad1 and <= Key.NumPad9 when e.KeyModifiers == KeyModifiers.None:
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
            if (RefineBox.IsFocused)
                vm.RefineCommand.Execute(null);
            else
                vm.ApplyCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: PopupItem item })
            ViewModel.RunCommand.Execute(item);
    }
}
