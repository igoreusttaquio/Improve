using Improve.Core.Resources;

namespace Improve.Core.Models;

/// <summary>Detecta atalhos repetidos entre o atalho global e os atalhos diretos das extensões ativas.</summary>
public static class HotkeyConflicts
{
    /// <summary>Nome da extensão que já usa o atalho, ou <c>null</c> se estiver livre.</summary>
    public static string? ForGlobal(Hotkey candidate, IEnumerable<Extension> extensions) =>
        FindExtension(candidate, extensions, ignoreId: null);

    /// <summary>Quem já usa o atalho (o atalho global ou outra extensão), ou <c>null</c> se estiver livre.</summary>
    public static string? ForExtension(Hotkey candidate, Guid extensionId, AppSettings settings, IEnumerable<Extension> extensions)
    {
        if (Hotkey.TryParse(settings.GlobalHotkey, out var global) && global == candidate)
            return CoreStrings.GlobalHotkeyOwner;
        return FindExtension(candidate, extensions, extensionId);
    }

    private static string? FindExtension(Hotkey candidate, IEnumerable<Extension> extensions, Guid? ignoreId) =>
        extensions.FirstOrDefault(e => e.Enabled && e.Id != ignoreId && Hotkey.TryParse(e.Hotkey, out var h) && h == candidate)?.Name;
}
