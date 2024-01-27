using LizardProtocol;

namespace Lizard;

public class IceDebugTarget : IDebugTarget
{
    public IceDebugTarget(DebugHostPrx host) => Host = host ?? throw new ArgumentNullException(nameof(host));
    public DebugHostPrx Host { get; }

    public Registers GetState() => Host.GetState();
    public byte[] GetMemory(Address addr, int bufferLength) => Host.GetMemory(addr, bufferLength); 
    public void Continue() => Host.Continue(); 
    public Registers Break() => Host.Break(); 
    public Registers StepIn() => Host.StepIn(); 
    public Registers StepOver() => Host.StepOver(); 
    public Registers StepMultiple(int i) => Host.StepMultiple(i); 
    public void RunToAddress(Address address) => Host.RunToAddress(address); 
    public AssemblyLine[] Disassemble(Address address, int length) => Host.Disassemble(address, length); 
    public void SetMemory(Address address, byte[] bytes) => Host.SetMemory(address, bytes); 
    public int GetMaxNonEmptyAddress(short segment) => Host.GetMaxNonEmptyAddress(segment);
    public IEnumerable<Address> SearchMemory(Address address, int length, byte[] toArray, int advance) => Host.SearchMemory(address, length, toArray, advance);
    public Breakpoint[] ListBreakpoints() => Host.ListBreakpoints(); 
    public void SetBreakpoint(Breakpoint bp) => Host.SetBreakpoint(bp); 
    public void EnableBreakpoint(int id, bool enable) => Host.EnableBreakpoint(id, enable); 
    public void DelBreakpoint(int id) => Host.DelBreakpoint(id); 
    public void SetRegister(Register reg, int value) => Host.SetRegister(reg, value); 
    public Descriptor[] GetGdt() => Host.GetGdt(); 
    public Descriptor[] GetLdt() => Host.GetLdt();
}