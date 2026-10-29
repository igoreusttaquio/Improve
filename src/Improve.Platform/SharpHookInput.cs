using Improve.Core.Execution;
using Improve.Core.Models;
using SharpHook;
using SharpHook.Data;
using SharpHook.Simulation;

namespace Improve.Platform;

/// <summary>
/// Atalhos globais e simulação de teclas com o SharpHook (Windows, macOS e X11). O hook roda numa thread
/// própria; os handlers são síncronos para poder suprimir a tecla do atalho antes que chegue ao app em foco.
/// </summary>
public sealed class SharpHookInput : IHotkeyService, IKeyboard
{
    private static readonly Dictionary<string, KeyCode> NamedKeys = new(StringComparer.Ordinal)
    {
        [","] = KeyCode.VcComma,
        ["."] = KeyCode.VcPeriod,
        ["-"] = KeyCode.VcMinus,
        ["="] = KeyCode.VcEquals,
        ["/"] = KeyCode.VcSlash,
        [";"] = KeyCode.VcSemicolon,
        ["'"] = KeyCode.VcQuote,
        ["["] = KeyCode.VcOpenBracket,
        ["]"] = KeyCode.VcCloseBracket,
        ["\\"] = KeyCode.VcBackslash,
        ["`"] = KeyCode.VcBackQuote,
    };

    private readonly SimpleGlobalHook _hook = new();
    private readonly EventSimulator _simulator = EventSimulator.Create("improve");
    private readonly Func<bool> _isOwnWindowFocused;
    private readonly Lock _gate = new();
    private Dictionary<(HotkeyModifiers, KeyCode), string> _hotkeys = [];
    private int _pressedModifiers; // HotkeyModifiers, atualizado pela thread do hook

    /// <param name="isOwnWindowFocused">Se o improve está em foco, os atalhos são ignorados (ex.: ao gravar um atalho nas configurações).</param>
    public SharpHookInput(Func<bool> isOwnWindowFocused)
    {
        _isOwnWindowFocused = isOwnWindowFocused;
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
    }

    public event EventHandler<HotkeyPressedEventArgs>? Pressed;

    public bool IsPaused { get; set; }

    public void SetHotkeys(IReadOnlyDictionary<string, Hotkey> hotkeys)
    {
        var map = new Dictionary<(HotkeyModifiers, KeyCode), string>();
        foreach (var (id, hotkey) in hotkeys)
        {
            if (ToKeyCode(hotkey.Key) is { } key)
                map.TryAdd((hotkey.Modifiers, key), id);
        }
        lock (_gate)
            _hotkeys = map;
    }

    public void Start() => _ = _hook.RunAsync();

    public Task SendCopyAsync(CancellationToken ct) => SendShortcutAsync(KeyCode.VcC);

    public Task SendPasteAsync(CancellationToken ct) => SendShortcutAsync(KeyCode.VcV);

    public async Task<bool> WaitForModifiersReleasedAsync(TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (Volatile.Read(ref _pressedModifiers) != 0)
        {
            if (DateTime.UtcNow >= deadline)
                return false;
            await Task.Delay(10, ct).ConfigureAwait(false);
        }
        return true;
    }

    public void Dispose() => _hook.Dispose();

    private Task SendShortcutAsync(KeyCode key)
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyCode.VcLeftMeta : KeyCode.VcLeftControl;
        _simulator.SimulateKeyPress(modifier);
        _simulator.SimulateKeyPress(key);
        _simulator.SimulateKeyRelease(key);
        _simulator.SimulateKeyRelease(modifier);
        return Task.CompletedTask;
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.IsEventSimulated)
            return;
        if (ModifierOf(e.Data.KeyCode) is { } modifier)
        {
            Interlocked.Or(ref _pressedModifiers, (int)modifier);
            return;
        }
        if (IsPaused)
            return;

        string? id;
        lock (_gate)
            _hotkeys.TryGetValue((ModifiersFrom(e.RawEvent.Mask), e.Data.KeyCode), out id);
        if (id is null || _isOwnWindowFocused())
            return;

        // Não deixa a tecla do atalho chegar ao app em foco; o trabalho segue fora da thread do hook.
        e.SuppressEvent = true;
        ThreadPool.QueueUserWorkItem(_ => Pressed?.Invoke(this, new HotkeyPressedEventArgs(id)));
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (!e.IsEventSimulated && ModifierOf(e.Data.KeyCode) is { } modifier)
            Interlocked.And(ref _pressedModifiers, ~(int)modifier);
    }

    private static HotkeyModifiers? ModifierOf(KeyCode key) => key switch
    {
        KeyCode.VcLeftControl or KeyCode.VcRightControl => HotkeyModifiers.Ctrl,
        KeyCode.VcLeftShift or KeyCode.VcRightShift => HotkeyModifiers.Shift,
        KeyCode.VcLeftAlt or KeyCode.VcRightAlt => HotkeyModifiers.Alt,
        KeyCode.VcLeftMeta or KeyCode.VcRightMeta => HotkeyModifiers.Meta,
        _ => null,
    };

    private static HotkeyModifiers ModifiersFrom(EventMask mask)
    {
        var result = HotkeyModifiers.None;
        if ((mask & EventMask.Ctrl) != 0)
            result |= HotkeyModifiers.Ctrl;
        if ((mask & EventMask.Shift) != 0)
            result |= HotkeyModifiers.Shift;
        if ((mask & EventMask.Alt) != 0)
            result |= HotkeyModifiers.Alt;
        if ((mask & EventMask.Meta) != 0)
            result |= HotkeyModifiers.Meta;
        return result;
    }

    /// <summary>Nome do <see cref="Hotkey"/> → código do SharpHook ("K" → VcK, "Space" → VcSpace, "," → VcComma).</summary>
    internal static KeyCode? ToKeyCode(string key) =>
        NamedKeys.TryGetValue(key, out var named) ? named
        : Enum.TryParse<KeyCode>("Vc" + key, ignoreCase: false, out var code) ? code
        : null;
}
