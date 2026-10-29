using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Improve.App.Resources;
using Improve.Core.Models;

namespace Improve.App.Controls;

/// <summary>
/// Campo que grava a combinação pressionada (ex.: <c>Ctrl+Shift+Space</c>). Backspace/Delete limpa; Esc e Tab saem.
/// </summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly StyledProperty<string?> HotkeyProperty =
        AvaloniaProperty.Register<HotkeyBox, string?>(nameof(Hotkey), defaultBindingMode: BindingMode.TwoWay);

    public HotkeyBox()
    {
        IsReadOnly = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        PlaceholderText = Strings.HotkeyPlaceholder;
    }

    public string? Hotkey
    {
        get => GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HotkeyProperty)
            Text = Hotkey;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var modifiers = ToModifiers(e.KeyModifiers);
        if (modifiers == HotkeyModifiers.None && e.Key is Key.Tab or Key.Escape)
        {
            base.OnKeyDown(e);
            return;
        }

        e.Handled = true;
        if (modifiers == HotkeyModifiers.None && e.Key is Key.Back or Key.Delete)
        {
            Hotkey = null;
            return;
        }

        if (KeyName(e.Key) is { } name)
            Hotkey = new Hotkey(modifiers, name).ToString();
        else if (modifiers != HotkeyModifiers.None)
            Text = new Hotkey(modifiers, "…").ToString();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        Text = Hotkey;
    }

    private static HotkeyModifiers ToModifiers(KeyModifiers keys)
    {
        var result = HotkeyModifiers.None;
        if (keys.HasFlag(KeyModifiers.Control))
            result |= HotkeyModifiers.Ctrl;
        if (keys.HasFlag(KeyModifiers.Alt))
            result |= HotkeyModifiers.Alt;
        if (keys.HasFlag(KeyModifiers.Shift))
            result |= HotkeyModifiers.Shift;
        if (keys.HasFlag(KeyModifiers.Meta))
            result |= HotkeyModifiers.Meta;
        return result;
    }

    /// <summary>Nome da tecla no formato do <see cref="Core.Models.Hotkey"/>; <c>null</c> para modificadores e teclas não suportadas.</summary>
    private static string? KeyName(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.F1 and <= Key.F24 => key.ToString(),
        Key.Space => "Space",
        Key.Enter => "Enter",
        Key.Up or Key.Down or Key.Left or Key.Right => key.ToString(),
        Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Insert => key.ToString(),
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemMinus => "-",
        Key.OemPlus => "=",
        Key.OemQuestion => "/",
        Key.OemSemicolon => ";",
        Key.OemQuotes => "'",
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        Key.OemPipe => "\\",
        Key.OemTilde => "`",
        _ => null,
    };
}
