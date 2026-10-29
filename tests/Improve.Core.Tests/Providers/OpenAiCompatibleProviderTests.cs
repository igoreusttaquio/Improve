using System.Net;
using System.Text.Json.Nodes;
using Improve.Core.Models;
using Improve.Core.Providers;
using Improve.Core.Tests.Fakes;

namespace Improve.Core.Tests.Providers;

public class OpenAiCompatibleProviderTests
{
    private static readonly ProviderConfig Config = new("deepseek-1", ProviderKind.OpenAi, "DeepSeek", "https://api.deepseek.test/v1");
    private static readonly AiRequest Request = new("modelo-y", "sys", [new(ChatRole.User, "u1"), new(ChatRole.Assistant, "a1"), new(ChatRole.User, "u2")], null, 512);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string> Collect(HttpAiProvider provider)
    {
        var text = "";
        await foreach (var chunk in provider.StreamAsync(Request, Ct))
            text += chunk;
        return text;
    }

    [Fact]
    public async Task Streams_until_done_and_sends_the_expected_request()
    {
        const string body =
            "data: {\"choices\":[{\"delta\":{\"role\":\"assistant\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"Bom \"}}]}\n\n" +
            ": keep-alive\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"dia\"},\"finish_reason\":null}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
            "data: [DONE]\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"ignorado\"}}]}\n\n";
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Sse(body, chunkSize: 3));
        var provider = new OpenAiCompatibleProvider(handler.CreateClient(), Config, "sk-ds");

        Assert.Equal("Bom dia", await Collect(provider));

        var sent = Assert.Single(handler.Requests);
        Assert.Equal("https://api.deepseek.test/v1/chat/completions", sent.Uri.ToString());
        Assert.Equal("Bearer sk-ds", sent.Headers["Authorization"]);
        var json = JsonNode.Parse(sent.Body!)!;
        Assert.Equal("modelo-y", json["model"]!.GetValue<string>());
        Assert.True(json["stream"]!.GetValue<bool>());
        Assert.Null(json["temperature"]);
        Assert.Null(json["max_tokens"]);
        var roles = json["messages"]!.AsArray().Select(m => m!["role"]!.GetValue<string>());
        Assert.Equal(["system", "user", "assistant", "user"], roles);
        Assert.Equal("sys", json["messages"]![0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task Works_without_key_for_local_servers()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Sse("data: [DONE]\n\n"));
        var provider = new OpenAiCompatibleProvider(handler.CreateClient(), Config with { BaseUrl = "http://localhost:11434/v1" }, apiKey: null);

        Assert.Equal("", await Collect(provider));
        Assert.False(handler.Requests[0].Headers.ContainsKey("Authorization"));
    }

    [Fact]
    public async Task Error_inside_stream_becomes_provider_exception()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Sse("data: {\"error\":{\"message\":\"Model not found\"}}\n\n"));
        var provider = new OpenAiCompatibleProvider(handler.CreateClient(), Config, "k");

        var ex = await Assert.ThrowsAsync<ProviderException>(() => Collect(provider));

        Assert.Equal(ProviderErrorKind.BadRequest, ex.Kind);
        Assert.Equal("DeepSeek recusou o pedido: Model not found", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ProviderErrorKind.InvalidKey)]
    [InlineData(HttpStatusCode.TooManyRequests, ProviderErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ProviderErrorKind.Unavailable)]
    [InlineData(HttpStatusCode.NotFound, ProviderErrorKind.BadRequest)]
    public async Task Http_errors_are_mapped(HttpStatusCode status, ProviderErrorKind expected)
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("{\"error\":{\"message\":\"x\"}}", status));
        var provider = new OpenAiCompatibleProvider(handler.CreateClient(), Config, "k");

        var ex = await Assert.ThrowsAsync<ProviderException>(() => Collect(provider));

        Assert.Equal(expected, ex.Kind);
    }

    [Fact]
    public async Task Lists_models()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("{\"object\":\"list\",\"data\":[{\"id\":\"z\"},{\"id\":\"y\"}]}"));
        var provider = new OpenAiCompatibleProvider(handler.CreateClient(), Config, "k");

        Assert.Equal(["y", "z"], await provider.ListModelsAsync(Ct));
        Assert.Equal("https://api.deepseek.test/v1/models", handler.Requests[0].Uri.ToString());
    }

    [Fact]
    public async Task Test_reports_success()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("{\"data\":[]}"));
        var provider = new OpenAiCompatibleProvider(handler.CreateClient(), Config, "k");

        var check = await provider.TestAsync(Ct);

        Assert.True(check.Ok);
        Assert.Null(check.ErrorMessage);
    }
}
