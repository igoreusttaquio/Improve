using Improve.Core.Execution;
using Improve.Core.Storage;

namespace Improve.Core.Tests.Fakes;

/// <summary>Área de transferência e teclado simulados: "copiar" põe <see cref="Selection"/> na área de transferência.</summary>
public sealed class FakeDesktop : IClipboard, IKeyboard
{
    public string? Text { get; set; }
    public string? Selection { get; set; }
    public List<string> Pasted { get; } = [];
    public List<string> Log { get; } = [];

    /// <summary>Executado logo depois do colar simulado (para simular o usuário copiando algo no meio-tempo).</summary>
    public Action? AfterPaste { get; set; }

    public Task<ClipboardSnapshot> SnapshotAsync(CancellationToken ct)
    {
        Log.Add("snapshot");
        return Task.FromResult(new ClipboardSnapshot(Text, Native: Text));
    }

    public Task RestoreAsync(ClipboardSnapshot snapshot, CancellationToken ct)
    {
        Log.Add("restore");
        Text = (string?)snapshot.Native;
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync(CancellationToken ct) => Task.FromResult(Text);

    public Task SetTextAsync(string text, CancellationToken ct)
    {
        Text = text;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct)
    {
        Text = null;
        return Task.CompletedTask;
    }

    public Task SendCopyAsync(CancellationToken ct)
    {
        Log.Add("copy");
        if (Selection is not null)
            Text = Selection;
        return Task.CompletedTask;
    }

    public Task SendPasteAsync(CancellationToken ct)
    {
        Log.Add("paste");
        Pasted.Add(Text ?? "");
        AfterPaste?.Invoke();
        return Task.CompletedTask;
    }

    public Task<bool> WaitForModifiersReleasedAsync(TimeSpan timeout, CancellationToken ct)
    {
        Log.Add("wait-modifiers");
        return Task.FromResult(true);
    }
}

public sealed class FakeFocus : IFocusService
{
    public FocusTarget? Current { get; set; } = new(42, 7, "Bloco de Notas");
    public bool RestoreResult { get; set; } = true;

    public FocusTarget? Capture() => Current;
    public Task<bool> RestoreAsync(FocusTarget target, CancellationToken ct) => Task.FromResult(RestoreResult);
}

public sealed class FakeSecrets : ISecretStore
{
    public Dictionary<string, string> Values { get; } = [];
    public bool IsSecure => true;

    public Task<string?> GetAsync(string providerId, CancellationToken ct = default) => Task.FromResult(Values.GetValueOrDefault(providerId));

    public Task SetAsync(string providerId, string secret, CancellationToken ct = default)
    {
        Values[providerId] = secret;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string providerId, CancellationToken ct = default)
    {
        Values.Remove(providerId);
        return Task.CompletedTask;
    }
}
