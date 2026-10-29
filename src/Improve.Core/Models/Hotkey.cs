using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Improve.Core.Models;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,

    /// <summary>Cmd no macOS, Win no Windows, Super no Linux.</summary>
    Meta = 8,
}

/// <summary>Combinação de teclas no formato texto do JSON: <c>Ctrl+Shift+Space</c>, <c>Cmd+Alt+K</c>.</summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, string Key)
{
    private static readonly Dictionary<string, HotkeyModifiers> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = HotkeyModifiers.Ctrl,
        ["Control"] = HotkeyModifiers.Ctrl,
        ["Alt"] = HotkeyModifiers.Alt,
        ["Option"] = HotkeyModifiers.Alt,
        ["Shift"] = HotkeyModifiers.Shift,
        ["Cmd"] = HotkeyModifiers.Meta,
        ["Command"] = HotkeyModifiers.Meta,
        ["Meta"] = HotkeyModifiers.Meta,
        ["Win"] = HotkeyModifiers.Meta,
        ["Super"] = HotkeyModifiers.Meta,
    };

    /// <summary>Teclas de função podem ser usadas sem modificador; as demais precisam de pelo menos um.</summary>
    public bool IsValid => Key.Length > 0 && (Modifiers != HotkeyModifiers.None || IsFunctionKey(Key));

    public static bool TryParse(string? text, [NotNullWhen(true)] out Hotkey? hotkey)
    {
        hotkey = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var modifiers = HotkeyModifiers.None;
        string? key = null;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries))
        {
            if (part.Length == 0)
                return false;
            if (ModifierNames.TryGetValue(part, out var modifier))
                modifiers |= modifier;
            else if (key is null)
                key = NormalizeKey(part);
            else
                return false;
        }
        if (key is null)
            return false;

        hotkey = new Hotkey(modifiers, key);
        return true;
    }

    /// <summary>Texto canônico. No macOS o modificador Meta aparece como Cmd, nos demais como Win.</summary>
    public string ToString(bool mac)
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl))
            parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
            parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
            parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Meta))
            parts.Add(mac ? "Cmd" : "Win");
        parts.Add(Key);
        return string.Join('+', parts);
    }

    public override string ToString() => ToString(OperatingSystem.IsMacOS());

    // Letras em maiúscula; demais nomes com a primeira letra maiúscula ("space" → "Space").
    private static string NormalizeKey(string key) =>
        key.Length == 1 ? key.ToUpperInvariant() : char.ToUpperInvariant(key[0]) + key[1..];

    private static bool IsFunctionKey(string key) =>
        key.Length > 1 && key[0] == 'F' && int.TryParse(key.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 24;
}
