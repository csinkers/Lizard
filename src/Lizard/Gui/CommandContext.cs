using GhidraProgramData;

namespace Lizard.Gui;

public class CommandContext
{
    public event Action? ExitRequested;

    public CommandContext(DebugSessionProvider sessionProvider, MemoryMapping mapping, SymbolStore symbols)
    {
        SessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
        Mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        Symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
    }

    public IDebugSession Session => SessionProvider.Session;
    public DebugSessionProvider SessionProvider { get; }
    public MemoryMapping Mapping { get; }
    public SymbolStore Symbols { get; }

    public Symbol? LookupSymbolForAddress(uint memoryAddress) =>
        Mapping.ToFile(memoryAddress) is var (fileOffset, _) 
            ? Symbols.Data?.LookupSymbol(fileOffset) 
            : null;

    public DumpFileState GetDumpState()
    {
        var r = Session.Registers;
        return new DumpFileState(new DumpRegisters
            {
                cs = r.cs, ds = r.ds, es = r.es, fs = r.fs, gs = r.gs, ss = r.ss,
                eax = r.eax, ebx = r.ebx, ecx = r.ecx, edx = r.edx,
                esi = r.esi, edi = r.edi,
                ebp = r.ebp, esp = r.esp, eip = r.eip,
                flags = r.flags,
            })
        {
            Mapping = Mapping.Serialize(),
            DataPath = Symbols.DataPath,
            CodePath = Symbols.CodePath
        };
    }

    public void Exit() => ExitRequested?.Invoke();
}