using System.Text;
using System.Text.Json.Nodes;
using Improve.Core.Models;
using Improve.Core.Storage;

namespace Improve.Core.Tests.Storage;

public class ExtensionPorterTests
{
    private static readonly Extension Sample = new()
    {
        Name = "Resumir", Icon = "list", Prompt = "Resuma.", ProviderId = "anthropic-1", Model = "m", Mode = OutputMode.Copy,
        Temperature = 0.2, Hotkey = "Ctrl+Alt+R", Order = 5,
    };

    [Fact]
    public void Export_omits_provider_and_hotkey()
    {
        var json = JsonNode.Parse(ExtensionPorter.Export([Sample]))!;

        var exported = json["extensions"]![0]!;
        Assert.Equal(1, json["version"]!.GetValue<int>());
        Assert.Null(exported["providerId"]);
        Assert.Null(exported["hotkey"]);
        Assert.Equal("Resumir", exported["name"]!.GetValue<string>());
        Assert.Equal("copy", exported["mode"]!.GetValue<string>());
    }

    [Fact]
    public void Import_assigns_new_ids_chosen_provider_and_order()
    {
        var imported = ExtensionPorter.Import(ExtensionPorter.Export([Sample, Sample with { Name = "Outra" }]), "openai-1", firstOrder: 4);

        Assert.Equal(["Resumir", "Outra"], imported.Select(e => e.Name));
        Assert.All(imported, e => Assert.NotEqual(Sample.Id, e.Id));
        Assert.All(imported, e => Assert.Equal("openai-1", e.ProviderId));
        Assert.Equal([4, 5], imported.Select(e => e.Order));
        Assert.Equal("Resuma.", imported[0].Prompt);
        Assert.Equal(0.2, imported[0].Temperature);
    }

    [Theory]
    [InlineData("não é json")]
    [InlineData("{\"version\":1,\"extensions\":[]}")]
    [InlineData("{\"version\":99,\"extensions\":[{\"name\":\"x\",\"prompt\":\"y\"}]}")]
    public void Import_rejects_invalid_files(string content) =>
        Assert.Throws<InvalidDataException>(() => ExtensionPorter.Import(Encoding.UTF8.GetBytes(content), null, 0));
}
