namespace Lizard;

public interface IDebugTargetProvider
{
    IDebugTarget? Host { get; }
    ProgramDataManager ProgramData { get; }
    event Action? Connected;
    event Action? Disconnected;
    event StoppedDelegate? Stopped;

    void Connect(string hostname, int port);
    void Disconnect();
    bool TryLock();
    void Unlock();
}