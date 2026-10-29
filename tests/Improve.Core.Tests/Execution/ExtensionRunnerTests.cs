using System.Text.Json.Nodes;
using Improve.Core.Execution;
using Improve.Core.Models;
using Improve.Core.Prompting;
using Improve.Core.Providers;
using Improve.Core.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Improve.Core.Tests.Execution;

public sealed class ExtensionRunnerTests : IDisposable
{
    private static readonly SelectionTimings Fast = new(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(1));
    private static readonly ProviderConfig Provider = new("openai-1", ProviderKind.OpenAi, "OpenAI", "https://api.test/v1");

    private static readonly Extension Improve = new()
    {
        Name = "Melhorar mensagem", Prompt = "Melhore.", ProviderId = "openai-1", Model = "m1", Mode = OutputMode.Replace,
    };

    private readonly FakeDesktop _desktop = new() { Text = "do usuário", Selection = "oi pessoal\n" };
    private readonly FakeFocus _focus = new();
    private readonly FakeSecrets _secrets = new() { Values = { ["openai-1"] = "sk-1" } };
    private FakeHttpHandler _http = new(_ => FakeHttpHandler.Sse(Stream("```\nOlá, ", "pessoal\n```")));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _http.Dispose();

    private static string Stream(params string[] chunks) =>
        string.Concat(chunks.Select(c => $"data: {{\"choices\":[{{\"delta\":{{\"content\":{JsonValue.Create(c).ToJsonString()}}}}}]}}\n\n")) + "data: [DONE]\n\n";

    private ExtensionRunner CreateRunner() => new(
        new ClipboardSelectionService(_desktop, _desktop, Fast), _focus, _desktop,
        new ExtensionGenerator(_secrets, new ProviderFactory(_http.CreateClient()), NullLogger<ExtensionGenerator>.Instance));

    [Fact]
    public async Task Capture_waits_for_modifiers_before_copying()
    {
        var capture = await CreateRunner().CaptureAsync(Ct);

        Assert.NotNull(capture);
        Assert.Equal("oi pessoal\n", capture.Text);
        Assert.Equal("do usuário", capture.PreviousClipboard);
        Assert.Equal("Bloco de Notas", capture.Focus?.AppName);
        Assert.True(_desktop.Log.IndexOf("wait-modifiers") < _desktop.Log.IndexOf("copy"));
    }

    [Fact]
    public async Task Capture_without_text_returns_null()
    {
        _desktop.Selection = "   ";

        Assert.Null(await CreateRunner().CaptureAsync(Ct));
        Assert.Equal("do usuário", _desktop.Text);
    }

    [Fact]
    public async Task Generate_streams_chunks_and_returns_cleaned_result()
    {
        var runner = CreateRunner();
        var session = ExtensionGenerator.CreateSession(Improve, [Provider], (await runner.CaptureAsync(Ct))!);
        var chunks = new List<string>();

        var result = await runner.GenerateAsync(session, chunks.Add, Ct);

        Assert.Equal(["```\nOlá, ", "pessoal\n```"], chunks);
        Assert.Equal("Olá, pessoal\n", result);
        Assert.Equal("Bearer sk-1", _http.Requests[0].Headers["Authorization"]);
        var body = JsonNode.Parse(_http.Requests[0].Body!)!;
        Assert.Equal(PromptBuilder.BaseInstruction + "\n\nMelhore.", body["messages"]![0]!["content"]!.GetValue<string>());
        Assert.Equal("<texto>\noi pessoal\n\n</texto>", body["messages"]![1]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task Apply_pastes_into_the_original_window_and_restores_clipboard()
    {
        var runner = CreateRunner();
        var session = ExtensionGenerator.CreateSession(Improve, [Provider], (await runner.CaptureAsync(Ct))!);
        await runner.GenerateAsync(session, null, Ct);

        Assert.Equal(ApplyOutcome.Applied, await runner.ApplyAsync(session, Ct));
        Assert.Equal(["Olá, pessoal\n"], _desktop.Pasted);
        Assert.Equal("do usuário", _desktop.Text);
    }

    [Fact]
    public async Task Apply_does_not_paste_when_the_window_changed()
    {
        var runner = CreateRunner();
        var session = ExtensionGenerator.CreateSession(Improve, [Provider], (await runner.CaptureAsync(Ct))!);
        await runner.GenerateAsync(session, null, Ct);
        _focus.RestoreResult = false;

        Assert.Equal(ApplyOutcome.WindowChanged, await runner.ApplyAsync(session, Ct));
        Assert.Empty(_desktop.Pasted);
    }

    [Fact]
    public async Task Refine_sends_original_previous_result_and_adjustment()
    {
        var runner = CreateRunner();
        var session = ExtensionGenerator.CreateSession(Improve, [Provider], (await runner.CaptureAsync(Ct))!);
        await runner.GenerateAsync(session, null, Ct);
        _http.Dispose();
        _http = new FakeHttpHandler(_ => FakeHttpHandler.Sse(Stream("Oi!")));
        runner = CreateRunner();

        var result = await runner.RefineAsync(session, "mais curto", null, Ct);

        Assert.Equal("Oi!\n", result);
        var messages = JsonNode.Parse(_http.Requests[0].Body!)!["messages"]!.AsArray();
        Assert.Equal(["system", "user", "assistant", "user"], messages.Select(m => m!["role"]!.GetValue<string>()));
        Assert.Equal("Olá, pessoal\n", messages[2]!["content"]!.GetValue<string>());
        Assert.Equal("Ajuste o resultado: mais curto", messages[3]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void Session_requires_provider_and_model()
    {
        var capture = new Capture("x", null, null);

        var noProvider = Assert.Throws<ExtensionConfigurationException>(() => ExtensionGenerator.CreateSession(Improve with { ProviderId = "sumiu" }, [Provider], capture));
        var noModel = Assert.Throws<ExtensionConfigurationException>(() => ExtensionGenerator.CreateSession(Improve with { Model = "" }, [Provider], capture));

        Assert.Contains("não tem provedor", noProvider.Message, StringComparison.Ordinal);
        Assert.Contains("Escolha um modelo", noModel.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_propagates()
    {
        _http.Dispose();
        _http = new FakeHttpHandler(_ => FakeHttpHandler.Hanging());
        var runner = CreateRunner();
        var session = ExtensionGenerator.CreateSession(Improve, [Provider], new Capture("x", null, null));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.GenerateAsync(session, null, cts.Token));
    }
}
