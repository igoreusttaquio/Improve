using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Improve.App.Services;

/// <summary>
/// Log em arquivo (<c>improve.log</c> na pasta de configuração), ligado com <c>IMPROVE_LOG=1</c>. As mensagens
/// do app só têm metadados: nunca chaves, texto selecionado ou resultado da IA.
/// </summary>
public sealed class FileLoggerProvider(string path) : ILoggerProvider
{
    private readonly Lock _gate = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName[(categoryName.LastIndexOf('.') + 1)..]);

    public void Dispose()
    {
    }

    private void Write(string line)
    {
        lock (_gate)
            File.AppendAllText(path, line + Environment.NewLine);
    }

    private sealed class FileLogger(FileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Write(string.Create(CultureInfo.InvariantCulture,
                $"{DateTime.Now:HH:mm:ss.fff} {logLevel,-11} {category}: {formatter(state, exception)}{(exception is null ? "" : " [" + exception.GetType().Name + "]")}"));
    }
}
