using System.Text.Json.Nodes;
using Improve.Core.Models;
using Improve.Core.Storage;

namespace Improve.Core.Tests.Storage;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "improve-tests", Guid.NewGuid().ToString("N"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private string FilePath(string name) => Path.Combine(_dir, name);

    [Fact]
    public async Task First_run_creates_defaults_with_four_example_extensions()
    {
        var store = new SettingsStore(_dir);

        var settings = await store.LoadSettingsAsync(Ct);
        var extensions = await store.LoadExtensionsAsync(Ct);

        Assert.Equal(["anthropic-1", "openai-1", "deepseek-1"], settings.Providers.Select(p => p.Id));
        Assert.Equal(
            [("Melhorar mensagem", OutputMode.Suggest), ("Deixar formal", OutputMode.Suggest), ("Traduzir para inglês", OutputMode.Replace), ("Refatorar código", OutputMode.Suggest)],
            extensions.Extensions.Select(e => (e.Name, e.Mode)));
        Assert.All(extensions.Extensions, e => Assert.Equal("anthropic-1", e.ProviderId));
        Assert.All(extensions.Extensions, e => Assert.Equal("", e.Model));
        Assert.True(File.Exists(FilePath("settings.json")));
        Assert.True(File.Exists(FilePath("extensions.json")));
    }

    [Fact]
    public async Task Writes_json_in_the_documented_format()
    {
        var store = new SettingsStore(_dir);
        await store.LoadExtensionsAsync(Ct);

        var settings = JsonNode.Parse(await File.ReadAllTextAsync(FilePath("settings.json"), Ct))!;
        var extensions = JsonNode.Parse(await File.ReadAllTextAsync(FilePath("extensions.json"), Ct))!;

        Assert.Equal(1, settings["version"]!.GetValue<int>());
        Assert.Equal("system", settings["theme"]!.GetValue<string>());
        Assert.Equal("anthropic", settings["providers"]![0]!["kind"]!.GetValue<string>());
        Assert.Equal("openai", settings["providers"]![1]!["kind"]!.GetValue<string>());
        Assert.Equal("https://api.anthropic.com", settings["providers"]![0]!["baseUrl"]!.GetValue<string>());
        Assert.Equal(1, extensions["version"]!.GetValue<int>());
        Assert.Equal("suggest", extensions["extensions"]![0]!["mode"]!.GetValue<string>());
        Assert.Equal("replace", extensions["extensions"]![2]!["mode"]!.GetValue<string>());
        Assert.Equal("wand-sparkles", extensions["extensions"]![0]!["icon"]!.GetValue<string>());
    }

    [Fact]
    public async Task Round_trips_and_leaves_no_temporary_file()
    {
        var store = new SettingsStore(_dir);
        var settings = (await store.LoadSettingsAsync(Ct)) with { GlobalHotkey = "Alt+Space", Theme = ThemePreference.Light };

        await store.SaveSettingsAsync(settings, Ct);
        var reloaded = await new SettingsStore(_dir).LoadSettingsAsync(Ct);

        Assert.Equal("Alt+Space", reloaded.GlobalHotkey);
        Assert.Equal(ThemePreference.Light, reloaded.Theme);
        Assert.Equal(settings.Providers, reloaded.Providers);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public async Task Corrupt_file_is_kept_aside_and_defaults_are_used()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(FilePath("settings.json"), "{ isto não é json", Ct);

        var settings = await new SettingsStore(_dir).LoadSettingsAsync(Ct);

        Assert.Equal(3, settings.Providers.Count);
        var backup = Assert.Single(Directory.GetFiles(_dir, "settings.json.corrompido-*"));
        Assert.Equal("{ isto não é json", await File.ReadAllTextAsync(backup, Ct));
    }

    [Fact]
    public async Task Newer_version_is_refused_without_touching_the_file()
    {
        Directory.CreateDirectory(_dir);
        const string json = "{\"version\": 99, \"globalHotkey\": \"F1\"}";
        await File.WriteAllTextAsync(FilePath("settings.json"), json, Ct);

        var ex = await Assert.ThrowsAsync<SettingsException>(() => new SettingsStore(_dir).LoadSettingsAsync(Ct));

        Assert.Contains("versão mais nova", ex.Message, StringComparison.Ordinal);
        Assert.Equal(json, await File.ReadAllTextAsync(FilePath("settings.json"), Ct));
    }

    [Fact]
    public async Task Missing_version_is_read_as_version_one()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(FilePath("settings.json"), "{\"globalHotkey\": \"F2\", \"providers\": []}", Ct);

        var settings = await new SettingsStore(_dir).LoadSettingsAsync(Ct);

        Assert.Equal("F2", settings.GlobalHotkey);
        Assert.Empty(settings.Providers);
    }
}
