using System.IO.Pipes;
using System.Text;

namespace Improve.App.Services;

/// <summary>
/// Instância única: o primeiro processo segura um mutex e escuta um named pipe (só do usuário atual);
/// os seguintes enviam seus argumentos para ele e saem.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private static readonly string Id = $"improve-{Environment.UserName}";

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    /// <summary><c>null</c> se já existe outra instância rodando.</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, $@"Local\{Id}", out var createdNew);
        if (createdNew)
            return new SingleInstance(mutex);
        mutex.Dispose();
        return null;
    }

    /// <summary>Envia os argumentos para a instância que já está rodando.</summary>
    public static void SendToRunning(string[] args)
    {
        using var pipe = new NamedPipeClientStream(".", Id, PipeDirection.Out, PipeOptions.CurrentUserOnly);
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
                await using var pipe = new NamedPipeServerStream(Id, PipeDirection.In, 1, PipeTransmissionMode.Byte,
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

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
