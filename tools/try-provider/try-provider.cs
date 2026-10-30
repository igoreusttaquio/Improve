// Testa um provedor de verdade, com streaming, usando o Improve.Core.
// A chave vem da variável de ambiente IMPROVE_API_KEY (opcional para Ollama).
//
// Uso (na raiz do repositório):
//   dotnet run tools/try-provider/try-provider.cs -- anthropic                     lista os modelos
//   dotnet run tools/try-provider/try-provider.cs -- openai https://api.deepseek.com/v1   lista os modelos de outra URL
//   dotnet run tools/try-provider/try-provider.cs -- anthropic <modelo> "texto"    roda "Melhorar mensagem"
//   dotnet run tools/try-provider/try-provider.cs -- openai <modelo> "texto" https://api.deepseek.com/v1
#:project ../../src/Improve.Core/Improve.Core.csproj
#:property PublishAot=false

using Improve.Core.Models;
using Improve.Core.Prompting;
using Improve.Core.Providers;

// Só a URL (sem modelo e texto): testa a conexão e lista os modelos daquele endereço.
var listOnly = args.Length == 1 || (args.Length == 2 && args[1].StartsWith("http", StringComparison.Ordinal));
if (args.Length == 0 || (args.Length == 2 && !listOnly) || args[0] is not ("anthropic" or "openai"))
{
    Console.Error.WriteLine("Uso: try-provider <anthropic|openai> [base-url] | <anthropic|openai> <modelo> \"texto\" [base-url]");
    return 2;
}

var kind = args[0] == "anthropic" ? ProviderKind.Anthropic : ProviderKind.OpenAi;
var explicitUrl = listOnly ? (args.Length == 2 ? args[1] : null) : (args.Length >= 4 ? args[3] : null);
var baseUrl = explicitUrl ?? (kind == ProviderKind.Anthropic ? "https://api.anthropic.com" : "https://api.openai.com/v1");
var config = new ProviderConfig("try", kind, kind == ProviderKind.Anthropic ? "Anthropic" : "OpenAI", baseUrl);
using var http = ProviderFactory.CreateHttpClient();
var provider = new ProviderFactory(http).Create(config, Environment.GetEnvironmentVariable("IMPROVE_API_KEY"));
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    if (listOnly)
    {
        var check = await provider.TestAsync(cts.Token);
        Console.WriteLine(check.Ok ? $"Conexão ok ({check.Latency.TotalMilliseconds:0} ms)." : check.ErrorMessage);
        if (check.Ok)
            foreach (var model in await provider.ListModelsAsync(cts.Token))
                Console.WriteLine(model);
        return check.Ok ? 0 : 1;
    }

    var selection = args[2];
    var (system, messages) = PromptBuilder.Build(
        "Reescreva a mensagem para ficar clara, cordial e sem erros, mantendo o idioma e o tom.", new PromptContext(selection));
    var raw = new System.Text.StringBuilder();
    var started = System.Diagnostics.Stopwatch.StartNew();
    await foreach (var chunk in provider.StreamAsync(new AiRequest(args[1], system, messages, null, 1024), cts.Token))
    {
        raw.Append(chunk);
        Console.Write(chunk);
    }
    Console.WriteLine();
    Console.WriteLine($"--- {started.ElapsedMilliseconds} ms; limpo: {ResponseCleaner.Clean(raw.ToString(), selection)}");
    return 0;
}
catch (ProviderException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelado.");
    return 130;
}
