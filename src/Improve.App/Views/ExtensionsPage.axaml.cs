using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
        }

        // O cartão vai para o espaço cujo centro está mais perto do ponteiro.
        var target = 0;
        for (var i = 1; i < _slots.Count; i++)
        {
            if (Math.Abs(_slots[i] - position.Y) < Math.Abs(_slots[target] - position.Y))
                target = i;
        }
        ViewModel.MoveLive(ViewModel.Cards.IndexOf(_pressed), target);
        e.Handled = true;
    }

    private async void OnCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var wasDragging = _dragging;
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
        ViewModel.MoveLive(from, from + (e.Key == Key.Up ? -1 : 1));
        e.Handled = true;
        await ViewModel.CommitOrderAsync();
    }
}
