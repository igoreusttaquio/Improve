using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Improve.App.Resources;

namespace Improve.App.Controls;

/// <summary>Tile com o ícone atual; ao clicar, abre a busca entre os nomes do Lucide.</summary>
public sealed class IconPicker : UserControl
{
    private const int MaxResults = 120;

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<IconPicker, string?>(nameof(Icon), defaultBindingMode: BindingMode.TwoWay);

    private readonly LucideIcon _current = new() { Size = 18 };
    private readonly TextBox _search = new() { PlaceholderText = Strings.IconSearch };
    private readonly WrapPanel _results = new() { Width = 352 };
    private readonly TextBlock _more = new() { Classes = { "caption", "muted" } };
    private readonly Flyout _flyout;

    public IconPicker()
    {
        var tile = new Border { Classes = { "tile" }, Child = _current };
        var button = new Button { Classes = { "ghost", "icon" }, Width = 40, Height = 40, Content = tile };
        ToolTip.SetTip(button, Strings.FieldIcon);

        _flyout = new Flyout
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            Content = new StackPanel
            {
                Spacing = 8,
                Children = { _search, new ScrollViewer { MaxHeight = 280, Content = _results }, _more },
            },
        };
        button.Flyout = _flyout;
        _flyout.Opened += (_, _) =>
        {
            _search.Text = "";
            Fill("");
            _search.Focus();
        };
        _search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                Fill(_search.Text ?? "");
        };
        Content = button;
    }

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
            _current.Icon = Icon;
    }

    private void Fill(string query)
    {
        var terms = query.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matches = LucideIcon.AvailableNames.Where(n => terms.All(t => n.Contains(t, StringComparison.Ordinal))).ToList();

        _results.Children.Clear();
        foreach (var name in matches.Take(MaxResults))
        {
            var item = new Button
            {
                Classes = { "ghost", "icon" },
                Margin = new Thickness(0, 0, 4, 4),
                Content = new LucideIcon { Icon = name, HorizontalAlignment = HorizontalAlignment.Center },
            };
            ToolTip.SetTip(item, name);
            item.Click += (_, _) =>
            {
                Icon = name;
                _flyout.Hide();
            };
            _results.Children.Add(item);
        }
        _more.IsVisible = matches.Count > MaxResults;
        _more.Text = string.Format(CultureInfo.CurrentCulture, Strings.IconMore, MaxResults, matches.Count);
    }
}
