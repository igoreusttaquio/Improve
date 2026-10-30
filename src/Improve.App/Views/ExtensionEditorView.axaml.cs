using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Improve.App.Views;

public partial class ExtensionEditorView : UserControl
{
    public ExtensionEditorView() => InitializeComponent();

    // Chips de variável: inserem {{...}} no cursor do prompt.
    private void OnInsertVariable(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string variable })
            PromptInput.Insert(variable);
    }
}
