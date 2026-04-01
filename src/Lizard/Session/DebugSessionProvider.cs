using Lizard.Comms;
using Lizard.Core;
using Lizard.Session.Dump;
using Lizard.Util;

namespace Lizard.Session;

public sealed class DebugSessionProvider : IDisposable
{
    static readonly DisconnectedSession DisconnectedSession = new();
    static readonly ITracer Log = new LogTopic(nameof(DebugSessionProvider));
    public event Action? Connected;
    public event Action? Disconnected;
    public event StoppedDelegate? Stopped;

    public IDebugSession Session { get; private set; } = DisconnectedSession;

    void OnConnected() => Connected?.Invoke();

    void OnDisconnected() => Disconnected?.Invoke();

    void OnStopped(LRegisters1 state) => Stopped?.Invoke(state);

    public void StartNetSession(string hostname, int port)
    {
        Disconnect();

        try
        {
            Session = NetDebugSession.Start(hostname, port);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to connect: {ex.Message}");
        }
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
