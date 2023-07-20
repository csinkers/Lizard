using Lizard.generated;
using Lizard.Gui.Windows;
using Action = System.Action;

namespace Lizard;

public class IceSessionManager
{
    readonly ReentrantLock _lock = new();
    IceSession? _ice;

    public IceSessionManager(ProjectManager projectManager, bool autoConnect)
    {
        projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        projectManager.ProjectLoaded += _ => Disconnect();

        if (autoConnect)
        {
            var project = projectManager.Project;
            var hostname = project.GetProperty(ConnectWindow.HostProperty)!;
            var port = project.GetProperty(ConnectWindow.PortProperty);
            Connect(hostname, port);
        }
    }

    public DebugHostPrx? Host => _ice?.DebugHost;
    public DebugClientPrx? Callback => _ice?.ClientProxy;
    public event Action? Connected;
    public event Action? Disconnected;
    public event StoppedDelegate? Stopped;

    public void Connect(string hostname, int port)
    {
        _lock.Lock();
        try
        {
            DisconnectInner();
            _ice = new IceSession(hostname, port);
            _ice.Client.StoppedEvent += OnStopped;
        }
        finally { _lock.Unlock(); }
        Connected?.Invoke();
    }

    public void Disconnect()
    {
        _lock.Lock();
        try
        {
            DisconnectInner();
        }
        finally { _lock.Unlock(); }
    }

    void DisconnectInner()
    {
        Disconnected?.Invoke();
        if (_ice != null)
        {
            _ice.Client.StoppedEvent -= OnStopped;
            _ice.Dispose();
        }

        _ice = null;
    }

    public bool TryLock() => _lock.TryLock();
    public void Unlock() => _lock.Unlock();


    void OnStopped(Registers state) => Stopped?.Invoke(state);
}