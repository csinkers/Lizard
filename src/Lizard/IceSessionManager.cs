using Lizard.generated;
using Lizard.Gui.Windows;
using Action = System.Action;

namespace Lizard;

public class IceSessionManager
{
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
        Disconnect();
        _ice = new IceSession(hostname, port);
        _ice.Client.StoppedEvent += OnStopped;
        Connected?.Invoke();
    }

    public void Disconnect()
    {
        Disconnected?.Invoke();
        if (_ice != null)
        {
            _ice.Client.StoppedEvent -= OnStopped;
            _ice.Dispose();
        }

        _ice = null;
    }

    void OnStopped(Registers state) => Stopped?.Invoke(state);
}
