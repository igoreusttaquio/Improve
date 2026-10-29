using Improve.Core.Storage;

namespace Improve.Core.Tests.Storage;

public sealed class ConfigStateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "improve-tests", Guid.NewGuid().ToString("N"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Updates_persist_and_raise_changed()
    {
        var state = new ConfigState(new SettingsStore(_dir));
        await state.LoadAsync(Ct);
        var changes = 0;
        state.Changed += (_, _) => changes++;

        await state.UpdateSettingsAsync(s => s with { GlobalHotkey = "Ctrl+Alt+I" }, Ct);
        var reloaded = new ConfigState(new SettingsStore(_dir));
        await reloaded.LoadAsync(Ct);

        Assert.Equal(1, changes);
        Assert.Equal("Ctrl+Alt+I", reloaded.Settings.GlobalHotkey);
    }

    [Fact]
    public async Task Extension_list_order_becomes_order_field()
    {
        var state = new ConfigState(new SettingsStore(_dir));
        await state.LoadAsync(Ct);

        await state.UpdateExtensionsAsync(list => list.Reverse(), Ct);
        await state.UpdateExtensionsAsync(list => list.Select(e => e.Name == "Deixar formal" ? e with { Enabled = false } : e), Ct);

        Assert.Equal(["Refatorar código", "Traduzir para inglês", "Melhorar mensagem"], state.EnabledExtensions.Select(e => e.Name));
        Assert.Equal([0, 1, 2, 3], state.Extensions.Extensions.Select(e => e.Order));
    }
}
