using Lizard.Interfaces;

namespace Lizard;

public delegate void StoppedDelegate(Registers state);

public class Debugger
{
    Registers _registers;
    long _version;

    public ITracer Log { get; }
    public DebugHostPrx Host { get; }
    public DebugClientPrx DebugClientPrx { get; }
    public Registers OldRegisters { get; private set; }
    public Registers Registers
    {
        get => _registers;
        private set { OldRegisters = _registers; _registers = value; }
    }

    public MemoryCache Memory { get; } = new();

    public Debugger(IceSession ice, ITracer log)
    {
        Log = log ?? throw new ArgumentNullException(nameof(log));
        ice.Client.StoppedEvent += Update;
        Host = ice.DebugHost;
        DebugClientPrx = ice.ClientProxy;
    }

    public bool TryFindSymbol(string name, out uint offset)
    {
        offset = 0;
        return false;
    }

    public void Update(Registers state)
    {
        Registers = state;
        _version++;
    }
}

