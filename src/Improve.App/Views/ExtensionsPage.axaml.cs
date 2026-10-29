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
    private ExtensionCardViewModel? _dragging;

    public ExtensionsPage() => InitializeComponent();

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

    // Clique no cartão abre o editor, exceto quando o clique foi no interruptor ou na alça.
    private void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<ToggleButton>(includeSelf: true) is null
            && source.FindAncestorOfType<Button>(includeSelf: true) is null
            && sender is Control { DataContext: ExtensionCardViewModel card })
            ViewModel.EditCommand.Execute(card);
    }

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: ExtensionCardViewModel card } grip && e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
        {
            _dragging = card;
            e.Pointer.Capture(grip);
            e.Handled = true;
        }
    }

    // Reordena ao vivo: o cartão vai para a posição cujo centro o ponteiro já passou.
    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging is null)
            return;
        var y = e.GetPosition(CardList).Y;
        var target = 0;
        foreach (var container in CardList.GetRealizedContainers())
        {
            if (container.DataContext != _dragging && container.Bounds.Center.Y < y)
                target++;
        }
        ViewModel.MoveLive(ViewModel.Cards.IndexOf(_dragging), Math.Min(target, ViewModel.Cards.Count - 1));
    }

    private async void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging is null)
            return;
        _dragging = null;
        e.Pointer.Capture(null);
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
