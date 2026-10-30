using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Improve.App.Resources;
using Improve.App.ViewModels;

namespace Improve.App.Views;

public partial class ExtensionsPage : UserControl
{
    private const double DragThreshold = 4;

    private ExtensionCardViewModel? _pressed;
    private Point _pressPoint;
    private bool _dragging;
    private bool _justDragged;
    private List<double> _slots = [];
    private int _startIndex;

    public ExtensionsPage()
    {
        InitializeComponent();
        CardList.AddHandler(PointerPressedEvent, OnCardPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        CardList.AddHandler(PointerMovedEvent, OnCardPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        CardList.AddHandler(PointerReleasedEvent, OnCardPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private ExtensionsPageViewModel ViewModel => (ExtensionsPageViewModel)DataContext!;

    private static readonly FilePickerFileType JsonType = new(Strings.JsonFileType) { Patterns = ["*.json"], MimeTypes = ["application/json"] };

    private async void OnImport(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { FileTypeFilter = [JsonType], AllowMultiple = false });
        if (files is not [var file])
            return;
        await using var stream = await file.OpenReadAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        ViewModel.StartImport(buffer.ToArray());
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = Strings.ExportFileName,
            DefaultExtension = "json",
            FileTypeChoices = [JsonType],
        });
        if (file is null)
            return;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(ViewModel.Export());
    }

    // Clique no cartão abre o editor, exceto quando foi no interruptor, na alça ou no fim de um arraste.
    private void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (_justDragged)
        {
            _justDragged = false;
            return;
        }
        if (e.Source is Visual source && source.FindAncestorOfType<ToggleButton>(includeSelf: true) is null
            && source.FindAncestorOfType<Button>(includeSelf: true) is null
            && sender is Control { DataContext: ExtensionCardViewModel card })
            ViewModel.EditCommand.Execute(card);
    }

    // Arraste pelo cartão inteiro. Os eventos são ouvidos no túnel e mesmo já tratados, porque
    // botões (como a alça) consomem o PointerPressed. O arraste só começa depois de DragThreshold.
    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(CardList).Properties.IsLeftButtonPressed
            || e.Source is not Control { DataContext: ExtensionCardViewModel card } source
            || source.FindAncestorOfType<ToggleButton>(includeSelf: true) is not null)
            return;
        _pressed = card;
        _pressPoint = e.GetPosition(CardList);
    }

    // Reordena ao vivo: o cartão vai para a posição cujo centro o ponteiro já passou.
    private void OnCardPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed is null)
            return;
        var position = e.GetPosition(CardList);
        if (!_dragging)
        {
            if (Math.Abs(position.Y - _pressPoint.Y) < DragThreshold && Math.Abs(position.X - _pressPoint.X) < DragThreshold)
                return;
            _dragging = true;
            _pressed.IsDragging = true;
            e.Pointer.Capture(CardList);
            // Centros dos espaços no início do arraste: durante o arraste o layout ainda não
            // refletiu as trocas, então os Bounds atuais não servem.
            _slots = [.. CardList.GetRealizedContainers().Select(c => c.Bounds.Center.Y).Order()];
            _startIndex = ViewModel.Cards.IndexOf(_pressed);
        }

        // O cartão vai para o espaço cujo centro está mais perto do ponteiro.
        var target = 0;
        for (var i = 1; i < _slots.Count; i++)
        {
            if (Math.Abs(_slots[i] - position.Y) < Math.Abs(_slots[target] - position.Y))
                target = i;
        }
        MoveAnimated(ViewModel.Cards.IndexOf(_pressed), target);

        // O cartão arrastado acompanha o ponteiro; o espaço dele já está em "target".
        if (CardList.ContainerFromItem(_pressed) is Control dragged)
        {
            dragged.ZIndex = 1;
            var transform = SlideTransform(dragged, animate: false);
            transform.Y = position.Y - _pressPoint.Y - (_slots[target] - _slots[_startIndex]);
        }
        e.Handled = true;
    }

    private async void OnCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var wasDragging = _dragging;
        if (wasDragging && _pressed is not null && CardList.ContainerFromItem(_pressed) is Control dragged)
        {
            // Encaixa no espaço com a mesma animação dos outros cartões.
            SlideTransform(dragged, animate: true).Y = 0;
            DispatcherTimer.RunOnce(() => dragged.ZIndex = 0, Duration);
        }
        _pressed?.IsDragging = false;
        _pressed = null;
        _dragging = false;
        if (!wasDragging)
            return;
        _justDragged = true;
        e.Pointer.Capture(null);
        e.Handled = true;
        await ViewModel.CommitOrderAsync();
    }
    // Alternativa pelo teclado: Alt+↑/↓ na alça.
    private async void OnGripKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Alt || e.Key is not (Key.Up or Key.Down)
            || sender is not Control { DataContext: ExtensionCardViewModel card })
            return;
        var from = ViewModel.Cards.IndexOf(card);
        MoveAnimated(from, from + (e.Key == Key.Up ? -1 : 1));
        e.Handled = true;
        await ViewModel.CommitOrderAsync();
    }

    // Troca de lugar animada (FLIP): guarda onde cada cartão aparecia, aplica a troca e faz cada um
    // deslizar da posição antiga até a nova. O cartão arrastado é tratado à parte.
    private void MoveAnimated(int from, int to)
    {
        if (from == to || to < 0 || to >= ViewModel.Cards.Count)
            return;
        var before = new Dictionary<ExtensionCardViewModel, double>();
        foreach (var card in ViewModel.Cards)
        {
            if (CardList.ContainerFromItem(card) is Control container)
                before[card] = container.Bounds.Y + ((container.RenderTransform as TranslateTransform)?.Y ?? 0);
        }

        ViewModel.MoveLive(from, to);
        CardList.UpdateLayout();

        foreach (var card in ViewModel.Cards)
        {
            if (card == _pressed && _dragging || CardList.ContainerFromItem(card) is not Control container || !before.TryGetValue(card, out var oldY))
                continue;
            var delta = oldY - container.Bounds.Y;
            if (Math.Abs(delta) < 0.5)
                continue;
            SlideTransform(container, animate: false).Y = delta;
            SlideTransform(container, animate: true).Y = 0;
        }
    }

    private TimeSpan Duration => this.TryFindResource("MotionFast", out var value) && value is TimeSpan duration ? duration : TimeSpan.FromMilliseconds(120);

    private TranslateTransform SlideTransform(Control container, bool animate)
    {
        if (container.RenderTransform is not TranslateTransform transform)
            container.RenderTransform = transform = new TranslateTransform();
        transform.Transitions = animate
            ? [new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = Duration,
                Easing = this.TryFindResource("MotionEase", out var easing) && easing is Easing ease ? ease : new CubicEaseOut(),
            }]
            : null;
        return transform;
    }
}
