using Avalonia.Controls;
using Improve.App.ViewModels;

namespace Improve.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow() => InitializeComponent();

    public SettingsWindow(SettingsViewModel viewModel)
        : this() => DataContext = viewModel;
}
