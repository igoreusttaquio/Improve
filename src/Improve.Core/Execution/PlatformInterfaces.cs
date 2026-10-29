namespace Improve.Core.Execution;

/// <summary>Conteúdo da área de transferência guardado para restaurar depois. <see cref="Native"/> é da implementação de SO.</summary>
public sealed record ClipboardSnapshot(string? Text, object? Native = null);

/// <summary>Área de transferência do SO.</summary>
public interface IClipboard
{
    Task<ClipboardSnapshot> SnapshotAsync(CancellationToken ct);
    Task RestoreAsync(ClipboardSnapshot snapshot, CancellationToken ct);
    Task<string?> GetTextAsync(CancellationToken ct);
    Task SetTextAsync(string text, CancellationToken ct);
    Task ClearAsync(CancellationToken ct);
}

/// <summary>Simulação de teclas no app em foco.</summary>
public interface IKeyboard
{
    /// <summary>Ctrl+C (Cmd+C no macOS).</summary>
    Task SendCopyAsync(CancellationToken ct);

    /// <summary>Ctrl+V (Cmd+V no macOS).</summary>
    Task SendPasteAsync(CancellationToken ct);

    /// <summary>Espera Ctrl/Shift/Alt/Cmd serem soltos. Devolve <c>false</c> se o tempo acabar antes.</summary>
    Task<bool> WaitForModifiersReleasedAsync(TimeSpan timeout, CancellationToken ct);
}

/// <summary>Janela/app que estava em foco quando o atalho foi pressionado.</summary>
public sealed record FocusTarget(nint Handle, int ProcessId, string? AppName);

public interface IFocusService
{
    /// <summary>Janela em foco agora, ou <c>null</c> se o SO não informar.</summary>
    FocusTarget? Capture();

    /// <summary>
    /// Devolve o foco ao alvo e confirma que ele está em primeiro plano. Devolve <c>false</c>, sem mudar o foco,
    /// se o usuário passou para outra janela que não seja do improve.
    /// </summary>
    Task<bool> RestoreAsync(FocusTarget target, CancellationToken ct);
}

/// <summary>Texto capturado e o texto que estava antes na área de transferência (para <c>{{clipboard}}</c>).</summary>
public sealed record SelectionCapture(string? Text, string? PreviousClipboard);

/// <summary>Captura e substituição da seleção no app em foco.</summary>
public interface ISelectionService
{
    Task<SelectionCapture> CaptureAsync(CancellationToken ct);

    /// <summary>Cola o texto no app em foco e depois restaura a área de transferência anterior.</summary>
    Task PasteAsync(string text, CancellationToken ct);

    /// <summary>Deixa o texto na área de transferência, sem restaurar.</summary>
    Task CopyAsync(string text, CancellationToken ct);
}
