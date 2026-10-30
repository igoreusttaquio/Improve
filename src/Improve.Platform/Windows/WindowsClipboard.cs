using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Improve.Core.Execution;
using static Improve.Platform.Windows.NativeMethods;

namespace Improve.Platform.Windows;

/// <summary>
/// Área de transferência pela API Win32. O instantâneo guarda todos os formatos em memória global
/// (texto, RTF, HTML, imagem DIB, arquivos), para devolver ao usuário exatamente o que ele tinha copiado.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsClipboard : IClipboard
{
    // Formatos que são handles GDI ou próprios do dono, não memória global: não dá para copiar os bytes.
    // CF_TEXT e CF_OEMTEXT são sintetizados pelo Windows a partir de CF_UNICODETEXT.
    private static readonly HashSet<uint> SkippedFormats = [1, 2, 3, 7, 9, 14, 0x80, 0x82, 0x83, 0x8E];

    public Task<ClipboardSnapshot> SnapshotAsync(CancellationToken ct) => Task.FromResult(WithClipboard(() =>
    {
        var formats = new List<(uint Format, byte[] Data)>();
        string? text = null;
        for (var format = EnumClipboardFormats(0); format != 0; format = EnumClipboardFormats(format))
        {
            if (SkippedFormats.Contains(format) || ReadGlobal(GetClipboardData(format)) is not { } data)
                continue;
            formats.Add((format, data));
            if (format == CF_UNICODETEXT)
                text = DecodeText(data);
        }
        return new ClipboardSnapshot(text, formats);
    }));

    public Task RestoreAsync(ClipboardSnapshot snapshot, CancellationToken ct)
    {
        WithClipboard(() =>
        {
            EmptyClipboard();
            if (snapshot.Native is List<(uint Format, byte[] Data)> formats)
            {
                foreach (var (format, data) in formats)
                    SetGlobal(format, data);
            }
            return true;
        });
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync(CancellationToken ct) =>
        Task.FromResult(WithClipboard(() => ReadGlobal(GetClipboardData(CF_UNICODETEXT)) is { } data ? DecodeText(data) : null));

    public Task SetTextAsync(string text, CancellationToken ct)
    {
        WithClipboard(() =>
        {
            EmptyClipboard();
            var bytes = new byte[(text.Length + 1) * 2];
            System.Text.Encoding.Unicode.GetBytes(text, bytes);
            SetGlobal(CF_UNICODETEXT, bytes);
            return true;
        });
        return Task.CompletedTask;
    }

    public long? ChangeCount => GetClipboardSequenceNumber();

    public Task ClearAsync(CancellationToken ct)
    {
        WithClipboard(EmptyClipboard);
        return Task.CompletedTask;
    }

    // Outro programa pode estar com a área de transferência aberta: tenta por até ~200 ms.
    private static T WithClipboard<T>(Func<T> action)
    {
        for (var attempt = 0; !OpenClipboard(0); attempt++)
        {
            if (attempt == 20)
                throw new IOException("A área de transferência está ocupada por outro programa.");
            Thread.Sleep(10);
        }
        try
        {
            return action();
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static byte[]? ReadGlobal(nint handle)
    {
        if (handle == 0)
            return null;
        var size = (int)GlobalSize(handle);
        var pointer = GlobalLock(handle);
        if (size == 0 || pointer == 0)
            return null;
        try
        {
            var data = new byte[size];
            Marshal.Copy(pointer, data, 0, size);
            return data;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private static void SetGlobal(uint format, byte[] data)
    {
        var handle = GlobalAlloc(GMEM_MOVEABLE, (nuint)data.Length);
        if (handle == 0)
            return;
        var pointer = GlobalLock(handle);
        Marshal.Copy(data, 0, pointer, data.Length);
        GlobalUnlock(handle);
        // Em caso de sucesso o sistema passa a ser dono da memória; em caso de falha, liberamos.
        if (SetClipboardData(format, handle) == 0)
            GlobalFree(handle);
    }

    private static string DecodeText(byte[] data)
    {
        var text = System.Text.Encoding.Unicode.GetString(data);
        var end = text.IndexOf('\0', StringComparison.Ordinal);
        return end < 0 ? text : text[..end];
    }
}
