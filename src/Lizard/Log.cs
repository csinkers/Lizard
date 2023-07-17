namespace Lizard;

public static class Log
{
    public static void Debug(string message) => LogHistory.Instance.Debug(message);
    public static void Info(string message) => LogHistory.Instance.Info(message);
    public static void Warn(string message) => LogHistory.Instance.Warn(message);
    public static void Error(string message) => LogHistory.Instance.Error(message);
}