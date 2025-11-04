using Lizard.Comms;
using Lizard.Memory;

namespace Lizard.Session;

public interface IDebugSession : IDisposable
{
    event Action? Disconnected;
    event StoppedDelegate? Stopped;

    bool CanRun { get; }
    bool IsPaused { get; }
    bool IsActive { get; }
    int Version { get; }
    LRegisters1 OldRegisters { get; }
    LRegisters1 Registers { get; }
    IMemoryCache Memory { get; }

    void Refresh();
    void Defer(IRequest request);
    void FlushDeferredResults();

    LRegisters1 GetState();
    byte[] GetMemory(LAddress1 addr, uint bufferLength);
    void Continue();
    LRegisters1 Break();
    LRegisters1 StepIn();
    LRegisters1 StepOver();
    LRegisters1 StepOut();
    LRegisters1 StepMultiple(uint i);
    void RunToAddress(LAddress1 address);
    List<LAssemblyLine1> Disassemble(LAddress1 address, uint length);
    void SetMemory(LAddress1 address, byte[] bytes);
    uint GetMaxNonEmptyAddress(ushort segment);
    IEnumerable<LAddress1> SearchMemory(LAddress1 address, uint length, byte[] toArray, uint advance);
    List<LBreakpoint1> ListBreakpoints();
    void SetBreakpoint(LBreakpoint1 bp);
    void EnableBreakpoint(uint id, bool enable);
    void DeleteBreakpoint(uint id);
    void SetRegister(LRegister1 reg, uint value);
    List<LDescriptor1> GetGdt();
    List<LDescriptor1> GetLdt();
}
