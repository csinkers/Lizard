using ImGuiColorTextEditNet;

namespace Lizard;

public class LogTopic : ITracer
{
    readonly string _category;

    public LogTopic(string category) => _category = category;

    public void Debug(string message) => LogHistory.Instance.Add(_category, message, Severity.Debug);

    public void Info(string message) => LogHistory.Instance.Add(_category, message, Severity.Info);

    public void Warn(string message) => LogHistory.Instance.Add(_category, message, Severity.Warn);

    public void Error(string message) => LogHistory.Instance.Add(_category, message, Severity.Error);

    public void Debug(Line line) => LogHistory.Instance.Add(_category, line, Severity.Debug);

    public void Info(Line line) => LogHistory.Instance.Add(_category, line, Severity.Info);

    public void Warn(Line line) => LogHistory.Instance.Add(_category, line, Severity.Warn);

    public void Error(Line line) => LogHistory.Instance.Add(_category, line, Severity.Error);
}
