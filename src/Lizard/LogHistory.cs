using ImGuiColorTextEditNet;
using Lizard.Gui.Windows;

namespace Lizard;

public class LogHistory
{
    const int MaxHistory = 10000;
    readonly object _syncRoot = new();
    readonly Queue<LogEntry> _history = new();
    public event Action<LogEntry>? EntryAdded;
    public event Action? Cleared;

    public static LogHistory Instance { get; } = new();
    LogHistory() { }

    public void Add(string category, string line) => Add(category, line, Severity.Debug);
    public void Add(string category, string line, Severity severity)
    {
        var l = new Line();
        var color = severity switch
        {
            Severity.Debug => CommandWindow.DebugColor,
            Severity.Info => CommandWindow.InfoColor,
            Severity.Warn => CommandWindow.WarningColor,
            Severity.Error => CommandWindow.ErrorColor,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
        };

        l.Append(color, line);
        Add(category, l, severity);
    }

    public void Add(string category, Line line, Severity severity)
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

    public void Debug(string category, string message) => Add(category, message, Severity.Debug);
    public void Info(string category, string message) => Add(category, message, Severity.Info);
    public void Warn(string category, string message) => Add(category, message, Severity.Warn);
    public void Error(string category, string message) => Add(category, message, Severity.Error);

    public void Debug(string category, Line line) => Add(category, line, Severity.Debug);
    public void Info(string category, Line line) => Add(category, line, Severity.Info);
    public void Warn(string category, Line line) => Add(category, line, Severity.Warn);
    public void Error(string category, Line line) => Add(category, line, Severity.Error);
}
