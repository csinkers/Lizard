using Lizard.Gui.Windows.Watch;
using LizardProtocol;

namespace Lizard;

public class DisconnectedSession : IDebugSession
{
    static readonly Registers _emptyRegisters = new(true, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public event Action? Disconnected;
    public event StoppedDelegate? Stopped;
    public bool CanRun => false;
    public bool IsPaused => true;
    public bool IsActive => false;
    public int Version => 1;
    public Registers OldRegisters => _emptyRegisters;
    public Registers Registers => _emptyRegisters;
    public IMemoryCache Memory { get; } = new EmptyMemoryCache();

    public void Refresh() { }

    public void Defer(IRequest request) => request.Execute(this);

    public void FlushDeferredResults() { }

    public void Continue() => throw new NotSupportedException("Disconnected session");

    public Registers Break() => throw new NotSupportedException("Disconnected session");

    public Registers StepIn() => throw new NotSupportedException("Disconnected session");

    public Registers StepOver() => throw new NotSupportedException("Disconnected session");

    public Registers StepOut() => throw new NotSupportedException("Disconnected session");

    public Registers StepMultiple(int i) => throw new NotSupportedException("Disconnected session");

    public void RunToAddress(Address address) => throw new NotSupportedException("Disconnected session");

    public void SetMemory(Address address, byte[] bytes) => throw new NotSupportedException("Disconnected session");

    public void SetBreakpoint(Breakpoint bp) => throw new NotSupportedException("Disconnected session");

    public void EnableBreakpoint(int id, bool enable) => throw new NotSupportedException("Disconnected session");

    public void DelBreakpoint(int id) => throw new NotSupportedException("Disconnected session");

    public void SetRegister(Register reg, int value) => throw new NotSupportedException("Disconnected session");

    public Breakpoint[] ListBreakpoints() => Array.Empty<Breakpoint>();

    public Registers GetState() => _emptyRegisters;

    public byte[] GetMemory(Address addr, int bufferLength) => Array.Empty<byte>();

    public AssemblyLine[] Disassemble(Address address, int length) => Array.Empty<AssemblyLine>();

    public int GetMaxNonEmptyAddress(short segment) => 0;

    public IEnumerable<Address> SearchMemory(Address address, int length, byte[] toArray, int advance) =>
        throw new NotSupportedException("Disconnected session");

    public Descriptor[] GetGdt() => throw new NotSupportedException("Disconnected session");

    public Descriptor[] GetLdt() => throw new NotSupportedException("Disconnected session");

    public void Dispose() { }
}
