namespace Lizard;

public class DebugHost
{
    readonly DebugHostPrx _host;

    public DebugHost(DebugHostPrx host) => _host = host ?? throw new ArgumentNullException(nameof(host));

    public void Connect(DebugClientPrx proxy)
    {
    }

    public void @Continue()
    {
        _host.Continue();
    }

    public Registers @Break()
    {
        return _host.Break();
    }

    public Registers StepIn()
    {
        return _host.StepIn();
    }

    public Registers StepMultiple(int cycles)
    {
        return _host.StepMultiple(cycles);
    }

    public void RunToAddress(Address address)
    {
        _host.RunToAddress(address);
    }

    public Registers GetState()
    {
        return _host.GetState();
    }

    public int GetMaxNonEmptyAddress(short seg)
    {
        return _host.GetMaxNonEmptyAddress(seg);
    }

    public Address[] SearchMemory(Address start, int length, byte[] pattern, int advance)
    {
        return _host.SearchMemory(start, length, pattern, advance);
    }

    public AssemblyLine[] Disassemble(Address address, int length)
    {
        return _host.Disassemble(address, length);
    }

    public byte[] GetMemory(Address address, int length)
    {
        return _host.GetMemory(address, length);
    }

    public void SetMemory(Address address, byte[] bytes)
    {
        _host.SetMemory(address, bytes);
    }

    public Breakpoint[] ListBreakpoints()
    {
        return _host.ListBreakpoints();
    }

    public void SetBreakpoint(Breakpoint breakpoint)
    {
        _host.SetBreakpoint(breakpoint);
    }

    public void DelBreakpoint(Address address)
    {

        _host.DelBreakpoint(address);
    }

    public void SetReg(Register reg, int value)
    {
        _host.SetReg(reg, value);
    }

    public Descriptor[] GetGdt()
    {
        return _host.GetGdt();
    }

    public Descriptor[] GetLdt()
    {
        return _host.GetLdt();
    }
}