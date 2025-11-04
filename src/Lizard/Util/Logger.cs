using Microsoft.Extensions.Logging;

namespace Lizard.Util;

public class Logger<T> : ILogger<T>
{
    readonly string _category = typeof(T).Name;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        var severity = logLevel switch
        {
            LogLevel.Information => Severity.Info,
            LogLevel.Warning => Severity.Warn,
            LogLevel.Error => Severity.Error,
            LogLevel.Critical => Severity.Error,
            _ => Severity.Debug
        };

        var message = formatter(state, exception);
        LogHistory.Instance.Add(_category, message, severity);
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;
}
