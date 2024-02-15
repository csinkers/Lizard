using Lizard.Gui;
using LizardProtocol;

namespace Lizard;

public sealed class DebugSessionProvider : IDisposable
{
    static readonly DisconnectedSession DisconnectedSession = new();
    public event Action? Connected;
    public event Action? Disconnected;
    public event StoppedDelegate? Stopped;

    public IDebugSession Session { get; private set; } = DisconnectedSession;

    void OnConnected() => Connected?.Invoke();

    void OnDisconnected() => Disconnected?.Invoke();

    void OnStopped(Registers state) => Stopped?.Invoke(state);

    public void StartIceSession(string hostname, int port)
    {
        Disconnect();
        Session = new IceDebugSession(hostname, port);
    }

    public void StartDumpSession(string path, CommandContext c)
    {
        Disconnect();
        var dumpFile = DumpFile.Load(path);
        Session = new DumpFileSession(dumpFile);
        c.ProjectManager.Load(dumpFile.State);
    }

    public void Disconnect()
    {
        if (Session == DisconnectedSession)
            return;

        Session.Disconnected -= OnDisconnected;
        Session.Stopped -= OnStopped;
        Session.Dispose();
        Session = DisconnectedSession;
    }

    public void Dispose() => Disconnect();
}
