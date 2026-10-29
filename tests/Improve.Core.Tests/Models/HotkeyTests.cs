using Improve.Core.Models;

namespace Improve.Core.Tests.Models;

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Shift+Space", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space")]
    [InlineData("cmd+shift+space", HotkeyModifiers.Meta | HotkeyModifiers.Shift, "Space")]
    [InlineData("Shift + Control + k", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "K")]
    [InlineData("Win+Alt+F5", HotkeyModifiers.Meta | HotkeyModifiers.Alt, "F5")]
    [InlineData("F9", HotkeyModifiers.None, "F9")]
    public void Parses_modifiers_in_any_order_and_case(string text, HotkeyModifiers modifiers, string key)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(new Hotkey(modifiers, key), hotkey);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl++A")]
    public void Rejects_invalid_text(string? text) => Assert.False(Hotkey.TryParse(text, out _));

    [Fact]
    public void Formats_in_canonical_order()
    {
        var hotkey = new Hotkey(HotkeyModifiers.Shift | HotkeyModifiers.Meta | HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "K");

        Assert.Equal("Ctrl+Alt+Shift+Cmd+K", hotkey.ToString(mac: true));
        Assert.Equal("Ctrl+Alt+Shift+Win+K", hotkey.ToString(mac: false));
    }

    [Theory]
    [InlineData("Ctrl+K", true)]
    [InlineData("F12", true)]
    [InlineData("K", false)]
    [InlineData("Space", false)]
    public void Requires_a_modifier_except_for_function_keys(string text, bool valid)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(valid, hotkey.IsValid);
    }

    [Fact]
    public void Global_conflicts_with_enabled_extensions_only()
    {
        Extension[] extensions =
        [
            new() { Name = "Traduzir", Prompt = "p", Hotkey = "Ctrl+Alt+T" },
            new() { Name = "Desligada", Prompt = "p", Hotkey = "Ctrl+Alt+D", Enabled = false },
        ];

        Assert.Equal("Traduzir", HotkeyConflicts.ForGlobal(new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "T"), extensions));
        Assert.Null(HotkeyConflicts.ForGlobal(new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "D"), extensions));
    }

    [Fact]
    public void Extension_conflicts_with_global_and_other_extensions_but_not_itself()
    {
        var settings = new AppSettings { GlobalHotkey = "Ctrl+Shift+Space" };
        var self = new Extension { Name = "Eu", Prompt = "p", Hotkey = "Ctrl+Alt+E" };
        Extension[] extensions = [self, new() { Name = "Outra", Prompt = "p", Hotkey = "Ctrl+Alt+O" }];

        Assert.Equal("atalho global", HotkeyConflicts.ForExtension(new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space"), self.Id, settings, extensions));
        Assert.Equal("Outra", HotkeyConflicts.ForExtension(new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "O"), self.Id, settings, extensions));
        Assert.Null(HotkeyConflicts.ForExtension(new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "E"), self.Id, settings, extensions));
    }
}
