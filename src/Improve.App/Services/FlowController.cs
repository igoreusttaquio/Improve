using Improve.Core.Execution;
using Improve.Core.Models;
using Improve.Core.Providers;
using Improve.Core.Storage;
using Improve.Platform;
using Microsoft.Extensions.Logging;

namespace Improve.App.Services;

/// <summary>
/// Liga os atalhos globais ao fluxo da seção 8: captura → IA → saída. Uma execução por vez.
/// </summary>
public sealed partial class FlowController(ConfigState config, IHotkeyService hotkeys, ExtensionRunner runner, ILogger<FlowController> logger)
    : IDisposable
{
    public const string GlobalHotkeyId = "global";

    private int _running;

    public bool IsPaused
    {
        get => hotkeys.IsPaused;
        set => hotkeys.IsPaused = value;
    }

    public void Start()
    {
        hotkeys.Pressed += (_, e) => _ = RunAsync(e.Id);
        config.Changed += (_, _) => RegisterHotkeys();
        RegisterHotkeys();
        hotkeys.Start();
    }

    /// <summary>O mesmo que o atalho global (usado por <c>improve --trigger</c>).</summary>
    public Task TriggerAsync() => RunAsync(GlobalHotkeyId);

    public void Dispose() => hotkeys.Dispose();

    private void RegisterHotkeys()
    {
        var map = new Dictionary<string, Hotkey>();
        if (Hotkey.TryParse(config.Settings.GlobalHotkey, out var global) && global.IsValid)
            map[GlobalHotkeyId] = global;
        foreach (var extension in config.EnabledExtensions)
        {
            if (Hotkey.TryParse(extension.Hotkey, out var hotkey) && hotkey.IsValid)
                map.TryAdd(extension.Id.ToString(), hotkey);
        }
        hotkeys.SetHotkeys(map);
    }

    private async Task RunAsync(string id)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
            return;
        try
        {
            // Provisório até o popup (fase 4): o atalho global executa a primeira extensão ativa.
            var enabled = config.EnabledExtensions;
            var extension = id == GlobalHotkeyId
                ? (enabled.Count > 0 ? enabled[0] : null)
                : enabled.FirstOrDefault(e => e.Id.ToString() == id);
            if (extension is null)
                return;

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var capture = await runner.CaptureAsync(timeout.Token).ConfigureAwait(false);
            if (capture is null)
            {
                LogNoSelection();
                return;
            }

            var session = ExtensionGenerator.CreateSession(extension, config.Settings.Providers, capture);
            await runner.GenerateAsync(session, onChunk: null, timeout.Token).ConfigureAwait(false);

            // Provisório até o painel de sugestão (fase 4): sugerir e "janela mudou" copiam o resultado.
            if (extension.Mode == OutputMode.Replace && await runner.ApplyAsync(session, timeout.Token).ConfigureAwait(false) == ApplyOutcome.Applied)
                return;
            await runner.CopyAsync(session, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ProviderException or ExtensionConfigurationException or OperationCanceledException or IOException)
        {
            // A mensagem pode citar o provedor, nunca o texto; a UI de erro vem na fase 4.
            LogFailed(ex.GetType().Name);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Atalho sem texto selecionado.")]
    private partial void LogNoSelection();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Execução pelo atalho falhou: {ErrorType}")]
    private partial void LogFailed(string errorType);
}
