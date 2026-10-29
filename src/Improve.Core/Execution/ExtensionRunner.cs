namespace Improve.Core.Execution;

public enum ApplyOutcome
{
    Applied,

    /// <summary>O usuário passou para outra janela: nada foi colado.</summary>
    WindowChanged,
}

/// <summary>Orquestra captura → IA → saída (seção 8 do plano). A UI decide quando chamar cada passo.</summary>
public sealed class ExtensionRunner(ISelectionService selection, IFocusService focus, IKeyboard keyboard, ExtensionGenerator generator)
{
    public static readonly TimeSpan ModifierReleaseTimeout = TimeSpan.FromMilliseconds(400);

    public ExtensionGenerator Generator { get; } = generator;

    /// <summary>Passos 1–4: registra o foco, espera soltar o atalho e copia a seleção. <c>null</c> se não houver texto.</summary>
    public async Task<Capture?> CaptureAsync(CancellationToken ct)
    {
        var target = focus.Capture();
        await keyboard.WaitForModifiersReleasedAsync(ModifierReleaseTimeout, ct).ConfigureAwait(false);
        var captured = await selection.CaptureAsync(ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(captured.Text) ? null : new Capture(captured.Text, captured.PreviousClipboard, target);
    }

    /// <summary>Passo 6 (e "Tentar de novo").</summary>
    public Task<string> GenerateAsync(RunSession session, Action<string>? onChunk, CancellationToken ct) =>
        Generator.GenerateAsync(session, onChunk, ct);

    public Task<string> RefineAsync(RunSession session, string instruction, Action<string>? onChunk, CancellationToken ct) =>
        Generator.RefineAsync(session, instruction, onChunk, ct);

    /// <summary>Passo 7a: volta ao app de origem e cola, só se a janela em foco não mudou.</summary>
    public async Task<ApplyOutcome> ApplyAsync(RunSession session, CancellationToken ct)
    {
        var result = session.Result ?? throw new InvalidOperationException("Não há resultado para aplicar.");
        if (session.Capture.Focus is not { } target || !await focus.RestoreAsync(target, ct).ConfigureAwait(false))
            return ApplyOutcome.WindowChanged;
        await selection.PasteAsync(result, ct).ConfigureAwait(false);
        return ApplyOutcome.Applied;
    }

    public Task CopyAsync(RunSession session, CancellationToken ct) =>
        selection.CopyAsync(session.Result ?? throw new InvalidOperationException("Não há resultado para copiar."), ct);
}
