using System.Diagnostics;
using System.Globalization;
using System.Text;
using Improve.Core.Models;
using Improve.Core.Prompting;
using Improve.Core.Providers;
using Improve.Core.Resources;
using Improve.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Improve.Core.Execution;

/// <summary>Seleção capturada no app de origem.</summary>
public sealed record Capture(string Text, string? PreviousClipboard, FocusTarget? Focus);

/// <summary>Uma execução de extensão sobre uma captura, com o histórico para Refinar e Tentar de novo.</summary>
public sealed class RunSession(Extension extension, ProviderConfig provider, Capture capture, AiRequest request)
{
    public Extension Extension { get; } = extension;
    public ProviderConfig Provider { get; } = provider;
    public Capture Capture { get; } = capture;
    public AiRequest Request { get; internal set; } = request;

    /// <summary>Último resultado já limpo.</summary>
    public string? Result { get; internal set; }
}

/// <summary>Extensão sem provedor ou modelo. A mensagem é para a UI.</summary>
public sealed class ExtensionConfigurationException(string message) : Exception(message);

/// <summary>A parte de IA de uma execução: monta o pedido, faz o streaming e limpa o resultado. Não depende do SO.</summary>
public sealed partial class ExtensionGenerator(ISecretStore secrets, IProviderFactory providers, ILogger<ExtensionGenerator> logger)
{
    public const int MaxTokens = 8192;

    public static RunSession CreateSession(Extension extension, IReadOnlyList<ProviderConfig> configured, Capture capture)
    {
        var provider = configured.FirstOrDefault(p => p.Id == extension.ProviderId)
            ?? throw new ExtensionConfigurationException(Format(CoreStrings.ErrorNoProvider, extension.Name));
        if (string.IsNullOrWhiteSpace(extension.Model))
            throw new ExtensionConfigurationException(Format(CoreStrings.ErrorNoModel, extension.Name));

        var (system, messages) = PromptBuilder.Build(extension.Prompt, new PromptContext(capture.Text, capture.Focus?.AppName, capture.PreviousClipboard));
        return new RunSession(extension, provider, capture, new AiRequest(extension.Model, system, messages, extension.Temperature, MaxTokens));
    }

    /// <summary>Chama o provedor em streaming (também usado por "Tentar de novo"). Devolve o resultado limpo.</summary>
    public async Task<string> GenerateAsync(RunSession session, Action<string>? onChunk, CancellationToken ct)
    {
        var apiKey = await secrets.GetAsync(session.Provider.Id, ct).ConfigureAwait(false);
        var provider = providers.Create(session.Provider, apiKey);
        var output = new StringBuilder();
        var watch = Stopwatch.StartNew();
        try
        {
            await foreach (var chunk in provider.StreamAsync(session.Request, ct).ConfigureAwait(false))
            {
                output.Append(chunk);
                onChunk?.Invoke(chunk);
            }
        }
        catch (ProviderException ex)
        {
            LogFailed(session.Provider.Name, session.Request.Model, ex.Kind, ex.StatusCode, watch.ElapsedMilliseconds);
            throw;
        }
        catch (OperationCanceledException)
        {
            LogCanceled(session.Provider.Name, session.Request.Model, watch.ElapsedMilliseconds);
            throw;
        }

        session.Result = ResponseCleaner.Clean(output.ToString(), session.Capture.Text);
        LogCompleted(session.Provider.Name, session.Request.Model, watch.ElapsedMilliseconds, session.Capture.Text.Length, session.Result.Length);
        return session.Result;
    }

    /// <summary>Refinar: acrescenta o último resultado e o ajuste pedido ao histórico e gera de novo.</summary>
    public Task<string> RefineAsync(RunSession session, string instruction, Action<string>? onChunk, CancellationToken ct)
    {
        if (session.Result is null)
            throw new InvalidOperationException("Não há resultado para refinar.");
        session.Request = PromptBuilder.Refine(session.Request, session.Result, instruction);
        return GenerateAsync(session, onChunk, ct);
    }

    private static string Format(string template, string arg) => string.Format(CultureInfo.CurrentCulture, template, arg);

    // Só metadados: nunca a chave, o texto selecionado ou o resultado.
    [LoggerMessage(Level = LogLevel.Information, Message = "Extensão concluída: {Provider}/{Model}, {DurationMs} ms, {InputLength} → {OutputLength} caracteres")]
    private partial void LogCompleted(string provider, string model, long durationMs, int inputLength, int outputLength);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Extensão falhou: {Provider}/{Model}, {Kind}, HTTP {StatusCode}, {DurationMs} ms")]
    private partial void LogFailed(string provider, string model, ProviderErrorKind kind, int? statusCode, long durationMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Extensão cancelada: {Provider}/{Model}, {DurationMs} ms")]
    private partial void LogCanceled(string provider, string model, long durationMs);
}
