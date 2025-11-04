using Gee.External.Capstone;
using Gee.External.Capstone.X86;
using Lizard.Comms;
using Lizard.Memory;

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
    public LRegisters1 OldRegisters { get; }
    public LRegisters1 Registers { get; }
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

    public LRegisters1 Break() => throw new NotSupportedException("Invalid operation when debugging a dump file");

    public LRegisters1 StepIn() => throw new NotSupportedException("Invalid operation when debugging a dump file");

    public LRegisters1 StepOver() => throw new NotSupportedException("Invalid operation when debugging a dump file");

    public LRegisters1 StepOut() => throw new NotSupportedException("Invalid operation when debugging a dump file");

    public LRegisters1 StepMultiple(uint i) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public void RunToAddress(LAddress1 address) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public void SetMemory(LAddress1 address, byte[] bytes) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public void SetBreakpoint(LBreakpoint1 bp) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public void EnableBreakpoint(uint id, bool enable) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public void DeleteBreakpoint(uint id) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public void SetRegister(LRegister1 reg, uint value) =>
        throw new NotSupportedException("Invalid operation when debugging a dump file");

    public List<LBreakpoint1> ListBreakpoints() => [];

    public LRegisters1 GetState() => Registers;

    public byte[] GetMemory(LAddress1 addr, uint bufferLength)
    {
        var result = new byte[bufferLength];
        _dump.Memory.AsSpan((int)addr.Offset, (int)bufferLength).CopyTo(result.AsSpan());
        return result;
    }

    public List<LAssemblyLine1> Disassemble(LAddress1 address, uint length)
    {
        var memory = GetMemory(address, length);
        var instructions = _disassembler.Disassemble(memory);
        var results = new List<LAssemblyLine1>(instructions.Length);

        for (var i = 0; i < instructions.Length; i++)
        {
            var instruction = instructions[i];
            var instrAddr = new LAddress1 { Segment = address.Segment, Offset = (uint)instruction.Address };
            var text = $"{instruction.Mnemonic} {instruction.Operand}";
            results[i] = new LAssemblyLine1
            {
                Address = instrAddr,
                Line = text,
                Bytes = instruction.Bytes
            };
        }

        return results;
    }

    public uint GetMaxNonEmptyAddress(ushort segment) => (uint)(_dump.Memory.Length - 1);

    public IEnumerable<LAddress1> SearchMemory(LAddress1 address, uint length, byte[] toArray, uint advance)
    {
        throw new NotImplementedException();
    }

    public List<LDescriptor1> GetGdt() => throw new NotImplementedException();

    public List<LDescriptor1> GetLdt() => throw new NotImplementedException();

    public void Dispose() => _disassembler.Dispose();

    static LRegisters1 ConvertRegisters(DumpRegisters r) =>
        new()
        {
            IsStopped = true,
            Flags = r.Flags,
            Eax = r.Eax,
            Ebx = r.Ebx,
            Ecx = r.Ecx,
            Edx = r.Edx,
            Esi = r.Esi,
            Edi = r.Edi,
            Ebp = r.Ebp,
            Esp = r.Esp,
            Eip = r.Eip,
            Es = r.Es,
            Cs = r.Cs,
            Ss = r.Ss,
            Ds = r.Ds,
            Fs = r.Fs,
            Gs = r.Gs
        };
}
