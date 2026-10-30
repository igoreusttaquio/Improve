namespace Improve.Core.Execution;

public sealed record SelectionTimings(TimeSpan PollInterval, TimeSpan CaptureTimeout, TimeSpan RestoreDelay)
{
    public static SelectionTimings Default { get; } = new(TimeSpan.FromMilliseconds(15), TimeSpan.FromMilliseconds(350), TimeSpan.FromMilliseconds(400));
}

/// <summary>
/// Seleção via área de transferência: simula copiar/colar e devolve ao usuário o que ele tinha copiado,
/// mas só se a área de transferência ainda contiver o que o improve pôs lá.
/// </summary>
public sealed class ClipboardSelectionService(IClipboard clipboard, IKeyboard keyboard, SelectionTimings? timings = null, TimeProvider? time = null)
    : ISelectionService
{
    private readonly SelectionTimings _timings = timings ?? SelectionTimings.Default;
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task<SelectionCapture> CaptureAsync(CancellationToken ct)
    {
        var previous = await clipboard.SnapshotAsync(ct).ConfigureAwait(false);
        await clipboard.ClearAsync(ct).ConfigureAwait(false);
        var cleared = clipboard.ChangeCount;
        await keyboard.SendCopyAsync(ct).ConfigureAwait(false);

        string? text = null;
        var start = _time.GetTimestamp();
        while (true)
        {
            // Com contador de mudanças, só abre a área de transferência quando ela mudou: abrir a cada
            // volta pode atrapalhar o app de origem justamente enquanto ele grava a cópia.
            if (cleared is null || clipboard.ChangeCount != cleared)
            {
                text = await clipboard.GetTextAsync(ct).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(text))
                    break;
            }
            if (_time.GetElapsedTime(start) >= _timings.CaptureTimeout)
                break;
            await Task.Delay(_timings.PollInterval, _time, ct).ConfigureAwait(false);
        }

        // A seleção já está em memória: devolve a área de transferência do usuário na hora.
        await RestoreIfUnchangedAsync(previous, text, ct).ConfigureAwait(false);
        return new SelectionCapture(string.IsNullOrEmpty(text) ? null : text, previous.Text);
    }

    public async Task PasteAsync(string text, CancellationToken ct)
    {
        var previous = await clipboard.SnapshotAsync(ct).ConfigureAwait(false);
        await clipboard.SetTextAsync(text, ct).ConfigureAwait(false);
        await keyboard.SendPasteAsync(ct).ConfigureAwait(false);
        // O app de destino lê a área de transferência de forma assíncrona: espera antes de restaurar.
        await Task.Delay(_timings.RestoreDelay, _time, ct).ConfigureAwait(false);
        await RestoreIfUnchangedAsync(previous, text, ct).ConfigureAwait(false);
    }

    public Task CopyAsync(string text, CancellationToken ct) => clipboard.SetTextAsync(text, ct);

    private async Task RestoreIfUnchangedAsync(ClipboardSnapshot previous, string? ours, CancellationToken ct)
    {
        var current = await clipboard.GetTextAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(current) ? string.IsNullOrEmpty(ours) : current == ours)
            await clipboard.RestoreAsync(previous, ct).ConfigureAwait(false);
    }
}
