namespace Lizard;

public class LogTopic : ITracer
{
    readonly string _category;
    public LogTopic(string category) => _category = category;

    public void Debug(string message) => LogHistory.Instance.Add(_category, message, Severity.Debug);
    public void Info(string message) => LogHistory.Instance.Add(_category, message, Severity.Info);
    public void Warn(string message) => LogHistory.Instance.Add(_category, message, Severity.Warn);
    public void Error(string message) => LogHistory.Instance.Add(_category, message, Severity.Error);
}