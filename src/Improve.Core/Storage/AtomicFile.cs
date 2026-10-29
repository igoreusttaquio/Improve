namespace Improve.Core.Storage;

public static class AtomicFile
{
    /// <summary>Grava num arquivo temporário ao lado do destino e o renomeia por cima, para nunca deixar um arquivo pela metade.</summary>
    public static async Task WriteAllBytesAsync(string path, ReadOnlyMemory<byte> content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await stream.WriteAsync(content, ct).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }
}
