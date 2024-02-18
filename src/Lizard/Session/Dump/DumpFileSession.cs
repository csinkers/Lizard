using Gee.External.Capstone;
using Gee.External.Capstone.X86;
using Lizard.Memory;
using LizardProtocol;

namespace Lizard.Session.Dump;

public sealed class DumpFileSession : IDebugSession, IMemoryReader
{
    readonly CapstoneX86Disassembler _disassembler;
    readonly DumpFile _dump;

    public event Action? Disconnected;
    public event StoppedDelegate? Stopped;
    public bool CanRun => false;
    public bool IsPaused => true;
    public bool IsActive => true;
    public Registers OldRegisters { get; }
    public Registers Registers { get; }
    public IMemoryCache Memory => new PassthroughMemoryCache(this);

    public void Refresh() { }

    public void Defer(IRequest request) => request.Execute(this);

    public void FlushDeferredResults() { }

    public int Version => 1;

    public DumpFileSession(DumpFile dump)
    {
        _dump = dump ?? throw new ArgumentNullException(nameof(dump));
        _disassembler = CapstoneDisassembler.CreateX86Disassembler(X86DisassembleMode.Bit32);
        _disassembler.DisassembleSyntax = DisassembleSyntax.Intel;
        Registers = ConvertRegisters(_dump.Registers);
        OldRegisters = Registers;
    }

    public void Read(uint offset, uint size, Span<byte> buffer) =>
        _dump.Memory.AsSpan((int)offset, (int)size).CopyTo(buffer);

    public void Continue() => throw new NotSupportedException("Invalid operation when debugging a dump file");

    public Registers Break() => throw new NotSupportedException("Invalid operation when debugging a dump file");

    public Registers StepIn() => throw new NotSupportedException("Invalid operation when debugging a dump file");

    public Registers StepOver() => throw new NotSupportedException("Invalid operation when debugging a dump file");

    public Registers StepOut() => throw new NotSupportedException("Invalid operation when debugging a dump file");

    public Registers StepMultiple(int i) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public void RunToAddress(Address address) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public void SetMemory(Address address, byte[] bytes) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public void SetBreakpoint(Breakpoint bp) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public void EnableBreakpoint(int id, bool enable) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public void DelBreakpoint(int id) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public void SetRegister(Register reg, int value) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public Breakpoint[] ListBreakpoints() => Array.Empty<Breakpoint>();

    public Registers GetState() => Registers;

    public byte[] GetMemory(Address addr, int bufferLength)
    {
        var result = new byte[bufferLength];
        _dump.Memory.AsSpan(addr.offset, bufferLength).CopyTo(result.AsSpan());
        return result;
    }

    public AssemblyLine[] Disassemble(Address address, int length)
    {
        var memory = GetMemory(address, length);
        var instructions = _disassembler.Disassemble(memory);
        var results = new AssemblyLine[instructions.Length];

        for (var i = 0; i < instructions.Length; i++)
        {
            var instruction = instructions[i];
            var instrAddr = new Address(address.segment, (int)instruction.Address);
            var text = $"{instruction.Mnemonic} {instruction.Operand}";
            results[i] = new AssemblyLine(instrAddr, text, instruction.Bytes);
        }

        return results;
    }

    public int GetMaxNonEmptyAddress(short segment) => _dump.Memory.Length - 1;

    public IEnumerable<Address> SearchMemory(Address address, int length, byte[] toArray, int advance)
    {
        throw new NotImplementedException();
    }

    public Descriptor[] GetGdt() => throw new NotImplementedException();

    public Descriptor[] GetLdt() => throw new NotImplementedException();

    public void Dispose() => _disassembler.Dispose();

    static Registers ConvertRegisters(DumpRegisters r) =>
        new(
            true,
            r.flags,
            r.eax,
            r.ebx,
            r.ecx,
            r.edx,
            r.esi,
            r.edi,
            r.ebp,
            r.esp,
            r.eip,
            (short)r.es,
            (short)r.cs,
            (short)r.ss,
            (short)r.ds,
            (short)r.fs,
            (short)r.gs
        );
}
