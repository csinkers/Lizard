using Lizard.Interfaces;

namespace Lizard;

public delegate void StoppedDelegate(Registers state);

public class Debugger : IMemoryReader
{
    Registers _registers;
    long _version;

    public event Action? ExitRequested;
    public ITracer Log { get; }
    public IceSessionManager SessionManager { get; }
    public DebugHostPrx? Host => SessionManager.Host;
    public DebugClientPrx? Callback => SessionManager.Callback;
    public IMemoryCache Memory { get; }
    public Registers OldRegisters { get; private set; }
    public Registers Registers
    {
        get => _registers;
        private set { OldRegisters = _registers; _registers = value; }
    }

    public bool IsPaused { get; private set; }

    public Debugger(IceSessionManager iceManager, ITracer log, IMemoryCache memory)
    {
        SessionManager = iceManager ?? throw new ArgumentNullException(nameof(iceManager));
        Log = log ?? throw new ArgumentNullException(nameof(log));
        Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        Memory.Reader = this;
        SessionManager.Connected += OnSessionManagerConnected;
        SessionManager.Stopped += Update;
    }

    void OnSessionManagerConnected()
    {
        Memory.Clear();
        if (Host != null)
            Update(Host.GetState());
    }

    public bool TryFindSymbol(string name, out uint offset)
    {
        offset = 0;
        return false;
    }

    public void Update(Registers state)
    {
        IsPaused = state.stopped;
        Registers = state;
        _version++;
    }

    public void Read(uint offset, byte[] buffer)
    {
        var host = Host;
        if (host == null)
            return;

        var addr = new Address(_registers.ds, (int)offset);
        var result = host.GetMemory(addr, buffer.Length);
        result.CopyTo(buffer, 0);
    }

    public void Exit() => ExitRequested?.Invoke();

    public void Dispose()
    {
    }
}

