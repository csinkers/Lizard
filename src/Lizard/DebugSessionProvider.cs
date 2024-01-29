using LizardProtocol;

namespace Lizard;

public class DebugSessionProvider : IDisposable
{
    static readonly DisconnectedSession _disconnectedSession = new();
    public event Action? Connected;
    public event Action? Disconnected;
    public event StoppedDelegate? Stopped;

    public IDebugSession Session { get; private set; } = _disconnectedSession;
    void OnConnected() => Connected?.Invoke();
    void OnDisconnected() => Disconnected?.Invoke();
    void OnStopped(Registers state) => Stopped?.Invoke(state);

    public void StartIceSession(string hostname, int port)
    {
        Disconnect();
        Session = new IceDebugSession(hostname, port);
    }

    public void Disconnect()
    {
        if (Session == _disconnectedSession)
            return;

        Session.Disconnected -= OnDisconnected;
        Session.Stopped -= OnStopped;
        Session.Dispose();
        Session = _disconnectedSession;
    }

    public void Dispose() => Disconnect();
}