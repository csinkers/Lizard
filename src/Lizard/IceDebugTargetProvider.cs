using LizardProtocol;

namespace Lizard;

public class IceDebugTargetProvider : IDebugTargetProvider, IDisposable
{
    readonly IceSessionManager _sessionManager;
    IceDebugTarget? _host;

    public ProgramDataManager ProgramData { get; }

    public event Action? Connected;
    public event Action? Disconnected;
    public event StoppedDelegate? Stopped;

    public IceDebugTargetProvider(IceSessionManager sessionManager, ProgramDataManager programDataManager)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        ProgramData = programDataManager ?? throw new ArgumentNullException(nameof(programDataManager));

        _sessionManager.Connected += OnConnected;
        _sessionManager.Disconnected += OnDisconnected;
        _sessionManager.Stopped += OnStopped;
    }

    void OnConnected() => Connected?.Invoke();
    void OnDisconnected() => Disconnected?.Invoke();
    void OnStopped(Registers state) => Stopped?.Invoke(state);

    public IDebugTarget? Host
    {
        get
        {
            var host = _sessionManager.Host;
            if (host == null)
                return null;

            if (_host?.Host != host)
                _host = new IceDebugTarget(host);

            return _host;
        }
    }

    public void Connect(string hostname, int port) => _sessionManager.Connect(hostname, port);
    public void Disconnect() => _sessionManager.Disconnect();
    public bool TryLock() => _sessionManager.TryLock();
    public void Unlock() => _sessionManager.Unlock();

    public void Dispose()
    {
        _sessionManager.Connected -= OnConnected;
        _sessionManager.Disconnected -= OnDisconnected;
        _sessionManager.Stopped -= OnStopped;
    }
}