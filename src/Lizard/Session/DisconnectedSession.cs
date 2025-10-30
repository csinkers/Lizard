using Lizard.Gui.Windows.Watch;
using Lizard.Memory;
using Lizard.Protocol.ProtocolGen;

namespace Lizard.Session;

public sealed class DisconnectedSession : IDebugSession
{
    static readonly LRegisters1 EmptyRegisters = new(true, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public event Action? Disconnected;
    public event StoppedDelegate? Stopped;
    public bool CanRun => false;
    public bool IsPaused => true;
    public bool IsActive => false;
    public int Version => 1;
    public LRegisters1 OldRegisters => EmptyRegisters;
    public LRegisters1 Registers => EmptyRegisters;
    public IMemoryCache Memory { get; } = new EmptyMemoryCache();

    public void Refresh() { }

    public void Defer(IRequest request) => request.Execute(this);

    public void FlushDeferredResults() { }

    public void Continue() => throw new NotSupportedException("Disconnected session");

    public LRegisters1 Break() => throw new NotSupportedException("Disconnected session");

    public LRegisters1 StepIn() => throw new NotSupportedException("Disconnected session");

    public LRegisters1 StepOver() => throw new NotSupportedException("Disconnected session");

    public LRegisters1 StepOut() => throw new NotSupportedException("Disconnected session");

    public LRegisters1 StepMultiple(uint i) => throw new NotSupportedException("Disconnected session");

    public void RunToAddress(LAddress1 address) => throw new NotSupportedException("Disconnected session");

    public void SetMemory(LAddress1 address, byte[] bytes) => throw new NotSupportedException("Disconnected session");

    public void SetBreakpoint(LBreakpoint1 bp) => throw new NotSupportedException("Disconnected session");

    public void EnableBreakpoint(uint id, bool enable) => throw new NotSupportedException("Disconnected session");

    public void DelBreakpoint(uint id) => throw new NotSupportedException("Disconnected session");

    public void SetRegister(LRegister1 reg, uint value) => throw new NotSupportedException("Disconnected session");

    public LBreakpoint1[] ListBreakpoints() => Array.Empty<LBreakpoint1>();

    public LRegisters1 GetState() => EmptyRegisters;

    public byte[] GetMemory(LAddress1 addr, uint bufferLength) => Array.Empty<byte>();

    public LAssemblyLine1[] Disassemble(LAddress1 address, uint length) => Array.Empty<LAssemblyLine1>();

    public uint GetMaxNonEmptyAddress(ushort segment) => 0;

    public IEnumerable<LAddress1> SearchMemory(LAddress1 address, uint length, byte[] toArray, uint advance) =>
        throw new NotSupportedException("Disconnected session");

    public LDescriptor1[] GetGdt() => throw new NotSupportedException("Disconnected session");

    public LDescriptor1[] GetLdt() => throw new NotSupportedException("Disconnected session");

    public void Dispose() { }
}
