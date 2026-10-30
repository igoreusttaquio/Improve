using Avalonia;
using Avalonia.Threading;
using Improve.App.Resources;
using Improve.App.ViewModels;
using Improve.App.Views;
using Improve.Core.Execution;
using Improve.Core.Models;
using Improve.Core.Prompting;
using Improve.Core.Providers;
using Improve.Core.Storage;
using Improve.Platform;
using Microsoft.Extensions.Logging;

namespace Improve.App.Services;

/// <summary>
/// O fluxo da seção 8: atalho → captura → popup (ou extensão direta) → IA → substituir, sugerir ou copiar.
/// Roda na thread de UI; o trabalho pesado fica nos serviços, que são assíncronos.
/// </summary>
public sealed partial class FlowController : IDisposable
{
    public const string GlobalHotkeyId = "global";
    private static readonly TimeSpan ToastDuration = TimeSpan.FromMilliseconds(1600);

    private readonly ConfigState _config;
    private readonly IHotkeyService _hotkeys;
    private readonly ExtensionRunner _runner;
    private readonly ICursorService _cursor;
    private readonly IWindowActivator _activator;
    private readonly ILogger<FlowController> _logger;
    private readonly DispatcherTimer _toastTimer;

    private PopupViewModel _popup = null!;
    private PopupWindow _window = null!;
    private PixelPoint _anchor;
    private Capture? _capture;
    private RunSession? _session;
    private Extension? _extension;
    private bool _fromActions;
    private CancellationTokenSource? _generation;
    private int _generationId;
    private bool _capturing;

    public FlowController(
        ConfigState config, IHotkeyService hotkeys, ExtensionRunner runner, ICursorService cursor, IWindowActivator activator,
        ILogger<FlowController> logger)
    {
        _config = config;
        _hotkeys = hotkeys;
        _runner = runner;
        _cursor = cursor;
        _activator = activator;
        _logger = logger;
        _toastTimer = new DispatcherTimer { Interval = ToastDuration };
        _toastTimer.Tick += (_, _) => Hide();
    }

    /// <summary>
    /// Abre as configurações: com <c>true</c>, no editor de nova extensão; com um id, no editor daquela
    /// extensão. Definido pelo App.
    /// </summary>
    public Action<bool, Guid?>? OpenSettings { get; set; }

    public bool IsPaused
    {
        get => _hotkeys.IsPaused;
        set => _hotkeys.IsPaused = value;
    }

    /// <summary>Cria a janela oculta (abertura instantânea depois) e registra os atalhos. Chamar na thread de UI.</summary>
    public void Start()
    {
        _popup = new PopupViewModel(new PopupActions(RunFromListAsync, ApplyAsync, CopyAsync, RetryAsync, RefineAsync, Close, Back, OpenSettingsFromPopup));
        _window = new PopupWindow(_popup);
        _window.FocusLost += (_, _) => OnFocusLost();
        // Aquece com a lista real, para os itens e fontes já estarem prontos no primeiro atalho.
        _popup.ShowActions(_config.EnabledExtensions, FreePromptBase());
        _window.WarmUp(() => _popup.State = PopupState.Hidden);

        _hotkeys.Pressed += (_, e) => Dispatcher.UIThread.Post(() => _ = OnHotkeyAsync(e.Id));
        _config.Changed += (_, _) => RegisterHotkeys();
        RegisterHotkeys();
        _hotkeys.Start();
    }

    /// <summary>O mesmo que o atalho global (usado por <c>improve --trigger</c>).</summary>
    public Task TriggerAsync() => OnHotkeyAsync(GlobalHotkeyId);

    public void Dispose()
    {
        _generation?.Cancel();
        _hotkeys.Dispose();
    }

    private void RegisterHotkeys()
    {
        var map = new Dictionary<string, Hotkey>();
        if (Hotkey.TryParse(_config.Settings.GlobalHotkey, out var global) && global.IsValid)
            map[GlobalHotkeyId] = global;
        foreach (var extension in _config.EnabledExtensions)
        {
            if (Hotkey.TryParse(extension.Hotkey, out var hotkey) && hotkey.IsValid)
                map.TryAdd(extension.Id.ToString(), hotkey);
        }
        _hotkeys.SetHotkeys(map);
    }

    // Passos 1–5: captura e abre o popup (atalho global) ou executa direto (atalho da extensão).
    private async Task OnHotkeyAsync(string id)
    {
        if (_capturing)
            return;
        _capturing = true;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        LogHotkey(id == GlobalHotkeyId ? "global" : "extensão");
        try
        {
            Hide();
            var position = _cursor.GetPosition();
            _anchor = new PixelPoint(position.X, position.Y);
            var capture = await _runner.CaptureAsync(CancellationToken.None);
            if (capture is null)
            {
                LogNoSelection();
                ShowToast(Strings.SelectTextFirst, "text-cursor-input");
                return;
            }
            _capture = capture;
            LogCaptured(capture.Text.Length, watch.ElapsedMilliseconds);

            _fromActions = false;
            if (id == GlobalHotkeyId)
            {
                _popup.ShowActions(_config.EnabledExtensions, FreePromptBase());
                Show(activate: true);
                LogPopupShown(watch.ElapsedMilliseconds);
            }
            else if (_config.EnabledExtensions.FirstOrDefault(e => e.Id.ToString() == id) is { } extension)
            {
                await RunAsync(extension);
            }
        }
        catch (IOException)
        {
            // Área de transferência ocupada por outro programa.
            ShowToast(Strings.SelectTextFirst, "text-cursor-input");
        }
        finally
        {
            _capturing = false;
        }
    }

    // O prompt livre usa o provedor e o modelo da primeira extensão (na ordem do popup) que já tem modelo escolhido.
    private Extension? FreePromptBase() =>
        _config.EnabledExtensions.Concat(_config.Extensions.Extensions.OrderBy(e => e.Order))
            .FirstOrDefault(e => e.Model.Length > 0 && _config.Settings.Providers.Any(p => p.Id == e.ProviderId));

    // Escolhida no popup: o painel ganha o botão de voltar para a lista.
    private Task RunFromListAsync(Extension extension)
    {
        _fromActions = true;
        return RunAsync(extension);
    }

    // Passos 6–7: executa a extensão escolhida no modo dela.
    private async Task RunAsync(Extension extension)
    {
        if (_capture is not { } capture)
            return;
        _extension = extension;
        var provider = _config.Settings.Providers.FirstOrDefault(p => p.Id == extension.ProviderId);
        RunSession session;
        try
        {
            session = ExtensionGenerator.CreateSession(extension, _config.Settings.Providers, capture);
            _session = session;
        }
        catch (ExtensionConfigurationException ex)
        {
            _session = null;
            _popup.ShowSuggestion(extension, provider?.Name, _fromActions, CodeLanguageFor(extension, capture));
            _popup.Error = ex.Message;
            Show(activate: true);
            return;
        }

        if (extension.Mode == OutputMode.Suggest)
        {
            _popup.ShowSuggestion(extension, provider?.Name, _fromActions, CodeLanguageFor(extension, capture));
            Show(activate: true);
            await GenerateAsync(() => GenerateStreamingAsync(session));
            return;
        }

        _popup.State = PopupState.Processing;
        Show(activate: true);
        if (!await GenerateAsync(() => GenerateStreamingAsync(session)))
        {
            // Falhou ou foi cancelado: o erro aparece no painel; o cancelamento só fecha.
            if (_popup.Error is not null)
            {
                var error = _popup.Error;
                _popup.ShowSuggestion(extension, provider?.Name, _fromActions, CodeLanguageFor(extension, capture));
                _popup.Error = error;
                Show(activate: true);
            }
            return;
        }

        if (extension.Mode == OutputMode.Copy)
        {
            await _runner.CopyAsync(session, CancellationToken.None);
            ShowToast(Strings.Copied, "check");
            return;
        }

        if (await _runner.ApplyAsync(session, HideAsync, CancellationToken.None) == ApplyOutcome.WindowChanged)
        {
            // Não cola numa janela diferente: mostra o resultado para Copiar ou Aplicar.
            var result = session.Result!;
            _popup.ShowSuggestion(extension, provider?.Name, _fromActions, CodeLanguageFor(extension, capture));
            _popup.Output = result;
            _popup.WindowChanged = true;
            Show(activate: true);
        }
    }

    /// <summary>Chama a IA com o token da geração atual; pedaços de uma geração antiga são descartados.</summary>
    private Task<string> GenerateStreamingAsync(RunSession session, string? refine = null)
    {
        var id = _generationId;
        void OnChunk(string chunk) => Dispatcher.UIThread.Post(() =>
        {
            if (id == _generationId)
                _popup.Output += chunk;
        });
        var token = _generation!.Token;
        return refine is null
            ? _runner.GenerateAsync(session, OnChunk, token)
            : _runner.RefineAsync(session, refine, OnChunk, token);
    }

    /// <summary>Roda uma geração cancelável; o texto final substitui o que veio em streaming. <c>false</c> se falhou ou foi cancelada.</summary>
    private async Task<bool> GenerateAsync(Func<Task<string>> generate)
    {
        _generation?.Cancel();
        _generation?.Dispose();
        _generation = new CancellationTokenSource();
        var id = ++_generationId;
        _popup.Output = "";
        _popup.Error = null;
        _popup.IsStreaming = true;
        try
        {
            var result = await generate();
            if (id == _generationId)
                _popup.Output = result;
            return true;
        }
        catch (ProviderException ex)
        {
            _popup.Error = ex.Message;
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            if (id == _generationId)
                _popup.IsStreaming = false;
        }
    }

    private async Task ApplyAsync()
    {
        if (_session?.Result is null)
        {
            LogApplyWithoutResult();
            return;
        }
        _popup.WindowChanged = false;
        var outcome = await _runner.ApplyAsync(_session, HideAsync, CancellationToken.None);
        LogApply(outcome);
        if (outcome == ApplyOutcome.WindowChanged)
        {
            _popup.WindowChanged = true;
            Show(activate: true);
        }
    }

    private async Task CopyAsync()
    {
        if (_session?.Result is null)
            return;
        await _runner.CopyAsync(_session, CancellationToken.None);
        Hide();
    }

    private Task RetryAsync() =>
        _session is { } session ? GenerateAsync(() => GenerateStreamingAsync(session)) : Task.CompletedTask;

    private Task RefineAsync(string instruction) =>
        _session is { Result: not null } session ? GenerateAsync(() => GenerateStreamingAsync(session, instruction)) : Task.CompletedTask;

    // Esc: cancela o que estiver em andamento, esquece a execução e fecha.
    private void Close() => Hide();

    // Fecha ao perder o foco, exceto durante o processamento ou o streaming do painel.
    private void OnFocusLost()
    {
        if (_popup.State is PopupState.Actions || (_popup.State is PopupState.Suggestion && !_popup.IsStreaming))
            Close();
    }

    // A linguagem escolhida na extensão, ou detectada no texto selecionado (o resultado costuma ser da mesma).
    private static string? CodeLanguageFor(Extension extension, Capture capture) =>
        extension.TreatAsCode ? extension.CodeLanguage ?? CodeLanguages.Detect(capture.Text) : null;

    // Volta do painel para a lista, com a mesma seleção, para escolher outra extensão.
    private void Back()
    {
        _generation?.Cancel();
        _generationId++;
        _session = null;
        _popup.ShowActions(_config.EnabledExtensions, FreePromptBase());
        Show(activate: true);
    }

    // Abre as configurações antes de esconder o popup: enquanto o popup está em foco, o improve pode trazer
    // outra janela sua para frente; depois de escondido, o Windows bloquearia.
    private void OpenSettingsFromPopup(bool createExtension)
    {
        var extensionId = createExtension ? null : _extension?.Id;
        OpenSettings?.Invoke(createExtension, extensionId);
        Close();
    }

    private void Show(bool activate)
    {
        _toastTimer.Stop();
        _window.ShowAt(_anchor, activate);
        if (activate && _window.TryGetPlatformHandle()?.Handle is { } handle)
            _activator.Activate(handle);
        if (activate)
            _window.FocusContent();
    }

    private void ShowToast(string text, string icon)
    {
        _popup.ShowToast(text, icon);
        _window.ShowAt(_anchor, activate: false);
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    /// <summary>
    /// Fecha e esquece a execução: cada seleção + extensão é um pedido novo, sem nada da anterior (nem a
    /// seleção, nem o resultado, nem o histórico do Refinar) guardado em memória.
    /// </summary>
    private void Hide()
    {
        _toastTimer.Stop();
        _generation?.Cancel();
        _generation?.Dispose();
        _generation = null;
        _generationId++;
        _capture = null;
        _session = null;
        _extension = null;
        _fromActions = false;
        _popup.Clear();
        _window.Hide();
    }

    private Task HideAsync() => Dispatcher.UIThread.InvokeAsync(Hide).GetTask();

    [LoggerMessage(Level = LogLevel.Information, Message = "Atalho sem texto selecionado.")]
    private partial void LogNoSelection();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Atalho recebido ({Kind}).")]
    private partial void LogHotkey(string kind);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Seleção capturada: {Length} caracteres em {ElapsedMs} ms.")]
    private partial void LogCaptured(int length, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Aplicar: {Outcome}.")]
    private partial void LogApply(ApplyOutcome outcome);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Aplicar sem resultado pronto.")]
    private partial void LogApplyWithoutResult();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Popup visível em {ElapsedMs} ms desde o atalho.")]
    private partial void LogPopupShown(long elapsedMs);
}
