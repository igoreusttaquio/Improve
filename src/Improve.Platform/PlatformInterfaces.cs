using Improve.Core.Models;

namespace Improve.Platform;

public sealed class HotkeyPressedEventArgs(string id) : EventArgs
{
    /// <summary>Identificador passado em <see cref="IHotkeyService.SetHotkeys"/>.</summary>
    public string Id { get; } = id;
}

/// <summary>Atalhos globais (fora do app).</summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>Disparado fora da thread de UI.</summary>
    event EventHandler<HotkeyPressedEventArgs>? Pressed;

    /// <summary>Com pausa, nenhum atalho dispara (e as teclas seguem para o app em foco).</summary>
    bool IsPaused { get; set; }

    /// <summary>Troca todos os atalhos registrados: id → combinação.</summary>
    void SetHotkeys(IReadOnlyDictionary<string, Hotkey> hotkeys);

    void Start();
}

/// <summary>Posição do cursor em pixels físicos da tela.</summary>
public interface ICursorService
{
    (int X, int Y) GetPosition();
}

/// <summary>Traz uma janela do improve para o primeiro plano, mesmo quando o SO trava a troca de foco.</summary>
public interface IWindowActivator
{
    void Activate(nint windowHandle);
}

/// <summary>Iniciar com o sistema.</summary>
public interface IAutostartService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

/// <summary>
/// Modo administrador. Um app comum não recebe teclas nem envia Ctrl+C para janelas elevadas (UIPI do Windows);
/// rodando elevado, o improve funciona também nelas.
/// </summary>
public interface IElevationService
{
    bool IsElevated { get; }

    /// <summary>Abre a instância elevada sem pedir confirmação; <c>false</c> se o modo não estiver registrado.</summary>
    bool TryStartElevated();

    /// <summary>Registra (ou atualiza) o modo; pede confirmação do SO se o improve não estiver elevado.</summary>
    Task<bool> RegisterAsync(bool startWithSystem);

    Task<bool> UnregisterAsync();
}
