namespace Lizard;

public class LogHistory : ITracer
{
    const int MaxHistory = 1000;
    readonly object _syncRoot = new();
    readonly Queue<LogEntry> _history = new();
    public event Action<LogEntry>? EntryAdded;
    public event Action? Cleared;

    public void Add(string line) => Add(line, Severity.Debug);
    public void Add(string line, Severity severity)
    {
        lock (_syncRoot)
        {
            LogEntry entry = new(severity, line);
            _history.Enqueue(entry);
            if (_history.Count > MaxHistory)
                _history.Dequeue();

            var handler = EntryAdded;
            handler?.Invoke(entry);
        }
    }

    public void Access<T>(T context, Action<T, IReadOnlyCollection<LogEntry>> operation)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        lock (_syncRoot)
            operation(context, _history);
    }

    public void Clear()
    {
        lock (_syncRoot)
            _history.Clear();
        Cleared?.Invoke();
    }

    public void Debug(string message) => Add(message, Severity.Debug);
    public void Info(string message) => Add(message, Severity.Info);
    public void Warn(string message) => Add(message, Severity.Warn);
    public void Error(string message) => Add(message, Severity.Error);
}
