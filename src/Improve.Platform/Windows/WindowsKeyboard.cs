using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Improve.Core.Execution;
using static Improve.Platform.Windows.NativeMethods;

namespace Improve.Platform.Windows;

/// <summary>
/// Ctrl+C/Ctrl+V com SendInput e espera dos modificadores com GetAsyncKeyState. Nativo em vez do simulador
/// do SharpHook: a primeira simulação do SharpHook demora a inicializar e perdia a primeira captura.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsKeyboard : IKeyboard
{
    private const ushort VK_CONTROL = 0x11;
    private static readonly int[] Modifiers = [0x10, 0x11, 0x12, 0x5B, 0x5C]; // Shift, Ctrl, Alt, Win esq./dir.

    public Task SendCopyAsync(CancellationToken ct) => SendShortcut(0x43); // C

    public Task SendPasteAsync(CancellationToken ct) => SendShortcut(0x56); // V

    public async Task<bool> WaitForModifiersReleasedAsync(TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (Modifiers.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0))
        {
            if (DateTime.UtcNow >= deadline)
                return false;
            await Task.Delay(10, ct).ConfigureAwait(false);
        }
        return true;
    }

    private static Task SendShortcut(ushort key)
    {
        INPUT[] inputs = [Key(VK_CONTROL, false), Key(key, false), Key(key, true), Key(VK_CONTROL, true)];
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        return Task.CompletedTask;
    }

    private static INPUT Key(ushort virtualKey, bool up) => new()
    {
        Type = INPUT_KEYBOARD,
        Data = new InputUnion { Keyboard = new KEYBDINPUT { VirtualKey = virtualKey, Flags = up ? KEYEVENTF_KEYUP : 0 } },
    };
}