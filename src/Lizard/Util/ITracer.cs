namespace Lizard.Util;

public interface ITracer
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}
