using System.Net;
using System.Text.Json.Nodes;
using Improve.Core.Models;
using Improve.Core.Providers;
using Improve.Core.Tests.Fakes;

namespace Improve.Core.Tests.Providers;

public class AnthropicProviderTests
{
    private static readonly ProviderConfig Config = new("anthropic-1", ProviderKind.Anthropic, "Anthropic", "https://api.anthropic.test/");
    private static readonly AiRequest Request = new("modelo-x", "sys", [new(ChatRole.User, "<texto>\noi\n</texto>")], 0.3, 512);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string> Collect(HttpAiProvider provider, CancellationToken? ct = null)
    {
        var text = "";
        await foreach (var chunk in provider.StreamAsync(Request, ct ?? Ct))
            text += chunk;
        return text;
    }

    private const string Stream =
        "event: message_start\ndata: {\"type\":\"message_start\",\"message\":{}}\n\n" +
        "event: content_block_start\ndata: {\"type\":\"content_block_start\",\"index\":0}\n\n" +
        "event: ping\ndata: {\"type\":\"ping\"}\n\n" +
        "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Olá, \"}}\n\n" +
        "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"mundo\"}}\n\n" +
        "event: message_stop\ndata: {\"type\":\"message_stop\"}\n\n";

    [Fact]
    public async Task Streams_text_deltas_and_sends_the_expected_request()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Sse(Stream, chunkSize: 5));
        var provider = new AnthropicProvider(handler.CreateClient(), Config, "sk-test");

        Assert.Equal("Olá, mundo", await Collect(provider));

        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://api.anthropic.test/v1/messages", sent.Uri.ToString());
        Assert.Equal("sk-test", sent.Headers["x-api-key"]);
        Assert.Equal("2023-06-01", sent.Headers["anthropic-version"]);
        var body = JsonNode.Parse(sent.Body!)!;
        Assert.Equal("modelo-x", body["model"]!.GetValue<string>());
        Assert.Equal(512, body["max_tokens"]!.GetValue<int>());
        Assert.Equal("sys", body["system"]!.GetValue<string>());
        Assert.True(body["stream"]!.GetValue<bool>());
        Assert.Equal(0.3, body["temperature"]!.GetValue<double>());
        Assert.Equal("user", body["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Equal("<texto>\noi\n</texto>", body["messages"]![0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task Stream_error_event_becomes_provider_exception()
    {
        const string body = "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"delta\":{\"text\":\"a\"}}\n\n" +
                            "event: error\ndata: {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"}}\n\n";
        var provider = new AnthropicProvider(new FakeHttpHandler(_ => FakeHttpHandler.Sse(body)).CreateClient(), Config, "k");

        var ex = await Assert.ThrowsAsync<ProviderException>(() => Collect(provider));

        Assert.Equal(ProviderErrorKind.Unavailable, ex.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ProviderErrorKind.InvalidKey)]
    [InlineData(HttpStatusCode.Forbidden, ProviderErrorKind.InvalidKey)]
    [InlineData(HttpStatusCode.TooManyRequests, ProviderErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, ProviderErrorKind.Unavailable)]
    [InlineData((HttpStatusCode)529, ProviderErrorKind.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, ProviderErrorKind.BadRequest)]
    public async Task Http_errors_are_mapped(HttpStatusCode status, ProviderErrorKind expected)
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("{\"type\":\"error\",\"error\":{\"type\":\"x\",\"message\":\"model: not found\"}}", status));
        var provider = new AnthropicProvider(handler.CreateClient(), Config, "k");

        var ex = await Assert.ThrowsAsync<ProviderException>(() => Collect(provider));

        Assert.Equal(expected, ex.Kind);
        Assert.Equal((int)status, ex.StatusCode);
        Assert.Contains("Anthropic", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_key_message_tells_where_to_fix()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("{}", HttpStatusCode.Unauthorized));
        var provider = new AnthropicProvider(handler.CreateClient(), Config, "k");

        var ex = await Assert.ThrowsAsync<ProviderException>(() => Collect(provider));

        Assert.Equal("Chave de API inválida para Anthropic. Abra Configurações > Provedores.", ex.Message);
    }

    [Fact]
    public async Task Missing_key_fails_without_calling_the_api()
    {
        var handler = new FakeHttpHandler(_ => throw new InvalidOperationException("não deveria chamar"));
        var provider = new AnthropicProvider(handler.CreateClient(), Config, apiKey: null);

        var ex = await Assert.ThrowsAsync<ProviderException>(() => Collect(provider));

        Assert.Equal(ProviderErrorKind.MissingKey, ex.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Network_failure_maps_to_no_network()
    {
        var handler = new FakeHttpHandler(_ => throw new HttpRequestException("dns"));
        var provider = new AnthropicProvider(handler.CreateClient(), Config, "k");

        var ex = await Assert.ThrowsAsync<ProviderException>(() => Collect(provider));

        Assert.Equal(ProviderErrorKind.NoNetwork, ex.Kind);
    }

    [Fact]
    public async Task Inactivity_timeout_maps_to_unavailable()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Hanging());
        var provider = new AnthropicProvider(handler.CreateClient(), Config, "k", TimeSpan.FromMilliseconds(100));

        var ex = await Assert.ThrowsAsync<ProviderException>(() => Collect(provider));

        Assert.Equal(ProviderErrorKind.Unavailable, ex.Kind);
    }

    [Fact]
    public async Task User_cancellation_is_not_translated()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Hanging());
        var provider = new AnthropicProvider(handler.CreateClient(), Config, "k");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cts.CancelAfter(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Collect(provider, cts.Token));
    }

    [Fact]
    public async Task Lists_models_across_pages()
    {
        var handler = new FakeHttpHandler(r => FakeHttpHandler.Json(r.RequestUri!.Query.Contains("after_id=b", StringComparison.Ordinal)
            ? "{\"data\":[{\"id\":\"c\"}],\"has_more\":false,\"last_id\":\"c\"}"
            : "{\"data\":[{\"id\":\"b\"},{\"id\":\"a\"}],\"has_more\":true,\"last_id\":\"b\"}"));
        var provider = new AnthropicProvider(handler.CreateClient(), Config, "k");

        Assert.Equal(["a", "b", "c"], await provider.ListModelsAsync(Ct));
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal("/v1/models", r.Uri.AbsolutePath));
    }

    [Fact]
    public async Task Test_reports_failure_message()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("{}", HttpStatusCode.Unauthorized));
        var provider = new AnthropicProvider(handler.CreateClient(), Config, "k");

        var check = await provider.TestAsync(Ct);

        Assert.False(check.Ok);
        Assert.Contains("inválida", check.ErrorMessage, StringComparison.Ordinal);
    }
}
