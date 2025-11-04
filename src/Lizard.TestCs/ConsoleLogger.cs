using CsConsole;
using Microsoft.Extensions.Logging;

namespace Lizard.TestCs;

internal class ConsoleLogger(IConsoleOutput output) : ILogger
{
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        var level = logLevel switch
        {
            LogLevel.Trace => "[TRACE]",
            LogLevel.Debug => "[DEBUG]",
            LogLevel.Information => "[INFO] ",
            LogLevel.Warning => "[WARN] ",
            LogLevel.Error => "[ERROR]",
            LogLevel.Critical => "[CRIT] ",
            _ => "[UNKN] ",
        };

        var msg = formatter(state, exception);
        output.WriteLine($"{level} {msg}");
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;
}
