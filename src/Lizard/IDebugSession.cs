using LizardProtocol;

namespace Lizard;

public interface IDebugSession : IDisposable
{
    event Action? Disconnected;
    event StoppedDelegate? Stopped;

    bool CanRun { get; }
    bool IsPaused { get; }
    bool IsActive { get; }
    int Version { get; }
    Registers OldRegisters { get; }
    Registers Registers { get; }
    IMemoryCache Memory { get; }

    void Refresh();
    void Defer(IRequest request);
    void FlushDeferredResults();

    Registers GetState();
    byte[] GetMemory(Address addr, int bufferLength);
    void Continue();
    Registers Break();
    Registers StepIn();
    Registers StepOver();
    Registers StepOut();
    Registers StepMultiple(int i);
    void RunToAddress(Address address);
    AssemblyLine[] Disassemble(Address address, int length);
    void SetMemory(Address address, byte[] bytes);
    int GetMaxNonEmptyAddress(short segment);
    IEnumerable<Address> SearchMemory(Address address, int length, byte[] toArray, int advance);
    Breakpoint[] ListBreakpoints();
    void SetBreakpoint(Breakpoint bp);
    void EnableBreakpoint(int id, bool enable);
    void DelBreakpoint(int id);
    void SetRegister(Register reg, int value);
    Descriptor[] GetGdt();
    Descriptor[] GetLdt();
}