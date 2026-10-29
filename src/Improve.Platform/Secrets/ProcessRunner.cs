using System.ComponentModel;
using System.Diagnostics;

namespace Improve.Platform.Secrets;

internal static class ProcessRunner
{
    /// <summary>Roda um executável sem shell. Devolve <c>null</c> se ele não existir.</summary>
    public static async Task<(int ExitCode, string Output)?> RunAsync(string fileName, IEnumerable<string> args, string? stdin, CancellationToken ct)
    {
        var info = new ProcessStartInfo(fileName)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);

        Process process;
        try
        {
            process = Process.Start(info) ?? throw new Win32Exception();
        }
        catch (Win32Exception)
        {
            return null;
        }

        using (process)
        {
            if (stdin is not null)
                await process.StandardInput.WriteAsync(stdin.AsMemory(), ct).ConfigureAwait(false);
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync(ct);
            _ = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return (process.ExitCode, await output.ConfigureAwait(false));
        }
    }
}
