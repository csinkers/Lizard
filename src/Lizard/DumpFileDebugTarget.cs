using Gee.External.Capstone;
using Gee.External.Capstone.X86;
using LizardProtocol;

namespace Lizard;

public class DumpFileDebugTarget : IDebugTarget, IDisposable
{
    readonly DumpFile _dump;
    readonly CapstoneX86Disassembler _disassembler;

    public DumpFileDebugTarget(DumpFile dump)
    {
        _dump = dump ?? throw new ArgumentNullException(nameof(dump));
        _disassembler = CapstoneDisassembler.CreateX86Disassembler(X86DisassembleMode.Bit32);
        _disassembler.DisassembleSyntax = DisassembleSyntax.Intel;
    }

    public void Continue() => throw new NotSupportedException("Invalid operation when debugging a dump file");
    public Registers Break() => throw new NotSupportedException("Invalid operation when debugging a dump file");
    public Registers StepIn() => throw new NotSupportedException("Invalid operation when debugging a dump file");
    public Registers StepOver() => throw new NotSupportedException("Invalid operation when debugging a dump file");
    public Registers StepMultiple(int i) => throw new NotSupportedException("Invalid operation when debugging a dump file");
    public void RunToAddress(Address address) => throw new NotSupportedException("Invalid operation when debugging a dump file");
    public void SetMemory(Address address, byte[] bytes) => throw new NotSupportedException("Invalid operation when debugging a dump file");
    public void SetBreakpoint(Breakpoint bp) => throw new NotSupportedException("Invalid operation when debugging a dump file");
    public void EnableBreakpoint(int id, bool enable) => throw new NotSupportedException("Invalid operation when debugging a dump file");
    public void DelBreakpoint(int id) => throw new NotSupportedException("Invalid operation when debugging a dump file");
    public void SetRegister(Register reg, int value) => throw new NotSupportedException("Invalid operation when debugging a dump file");
    public Breakpoint[] ListBreakpoints() => Array.Empty<Breakpoint>();

    public Registers GetState() => _dump.Registers;

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
}