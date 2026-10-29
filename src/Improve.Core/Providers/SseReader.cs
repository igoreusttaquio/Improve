using System.Runtime.CompilerServices;
using System.Text;

namespace Improve.Core.Providers;

public sealed record SseEvent(string? Event, string Data);

/// <summary>Leitor mínimo de Server-Sent Events: campos <c>event</c> e <c>data</c>, despacho na linha vazia.</summary>
public static class SseReader
{
    public static async IAsyncEnumerable<SseEvent> ReadAsync(Stream stream, [EnumeratorCancellation] CancellationToken ct = default)
    {
        // ReadLineAsync remonta linhas que chegam quebradas entre pedaços da rede e aceita \n, \r\n e \r.
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? eventName = null;
        var data = new StringBuilder();
        var hasData = false;

        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                if (hasData)
                    yield return new SseEvent(eventName, data.ToString());
                eventName = null;
                data.Clear();
                hasData = false;
                continue;
            }
            if (line[0] == ':')
                continue;

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            var field = colon < 0 ? line : line[..colon];
            var value = colon < 0 ? "" : line[(colon + 1)..];
            if (value.StartsWith(' '))
                value = value[1..];

            switch (field)
            {
                case "event":
                    eventName = value;
                    break;
                case "data":
                    if (hasData)
                        data.Append('\n');
                    data.Append(value);
                    hasData = true;
                    break;
            }
        }

        // Fim do stream sem a linha vazia final: despacha o que ficou.
        if (hasData)
            yield return new SseEvent(eventName, data.ToString());
    }
}
