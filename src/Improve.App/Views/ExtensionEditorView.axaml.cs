using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Improve.App.Views;

public partial class ExtensionEditorView : UserControl
{
    public ExtensionEditorView() => InitializeComponent();

    // Ao entrar no campo de modelo, abre a lista: quem não sabe o ID vê as opções sem procurar a setinha.
    private void OnModelFocused(object? sender, FocusChangedEventArgs e)
    {
        if (ModelBox.ItemsSource?.Cast<object>().Any() == true && !ModelBox.IsDropDownOpen)
            ModelBox.IsDropDownOpen = true;
    }

    // Chips de variável: inserem {{...}} no cursor do prompt.
    private void OnInsertVariable(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string variable })
            PromptInput.Insert(variable);
    }
}
