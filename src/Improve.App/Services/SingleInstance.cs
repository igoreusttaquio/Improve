using System.IO.Pipes;
using System.Text;

namespace Improve.App.Services;

/// <summary>
/// Instância única: o primeiro processo segura um mutex e escuta um named pipe (só do usuário atual);
/// os seguintes enviam seus argumentos para ele e saem.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _id;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex, string id)
    {
        _mutex = mutex;
        _id = id;
    }

    /// <summary>
    /// <c>null</c> se já existe outra instância com a mesma pasta de configuração (uma pasta diferente, como a de
    /// testes em <c>IMPROVE_CONFIG_DIR</c>, roda como instância separada).
    /// </summary>
    public static SingleInstance? TryAcquire(string configDirectory)
    {
        var id = IdFor(configDirectory);
        var mutex = new Mutex(initiallyOwned: true, $@"Local\{id}", out var createdNew);
        if (createdNew)
            return new SingleInstance(mutex, id);
        mutex.Dispose();
        return null;
    }

    /// <summary>Envia os argumentos para a instância que já está rodando com a mesma pasta de configuração.</summary>
    public static void SendToRunning(string configDirectory, string[] args)
    {
        using var pipe = new NamedPipeClientStream(".", IdFor(configDirectory), PipeDirection.Out, PipeOptions.CurrentUserOnly);
        pipe.Connect(timeout: 2000);
        pipe.Write(Encoding.UTF8.GetBytes(string.Join('\n', args)));
    }

    /// <summary>Recebe os argumentos de cada novo processo (fora da thread de UI).</summary>
    public void Listen(Action<string[]> onArguments) => _ = Task.Run(async () =>
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(_id, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                using var reader = new StreamReader(pipe, Encoding.UTF8);
                var text = await reader.ReadToEndAsync(_stop.Token).ConfigureAwait(false);
                onArguments(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // Cliente desconectou no meio: espera o próximo.
            }
        }
    });

    private static string IdFor(string configDirectory)
    {
        var path = Path.GetFullPath(configDirectory).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..12];
        return $"improve-{Environment.UserName}-{hash}";
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
