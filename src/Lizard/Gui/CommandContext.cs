using System.Runtime.InteropServices;
using GhidraProgramData;
using GhidraProgramData.Types;
using Lizard.Config;

namespace Lizard.Gui;

public class CommandContext
{
    static readonly LogTopic Log = new("Context");
    public event Action? ExitRequested;
    List<StackFrame> _stack = new();
    int _lastStackVersion = -1;

    public CommandContext(
        DebugSessionProvider sessionProvider,
        MemoryMapping mapping,
        SymbolStore symbols,
        ProjectManager projectManager
    )
    {
        SessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
        Mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        Symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
        ProjectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
    }

    public IDebugSession Session => SessionProvider.Session;
    public DebugSessionProvider SessionProvider { get; }
    public MemoryMapping Mapping { get; }
    public SymbolStore Symbols { get; }
    public ProjectManager ProjectManager { get; }

    public Symbol? LookupSymbolForAddress(uint memoryAddress) => LookupSymbolForAddress(memoryAddress, out _);

    public Symbol? LookupSymbolForAddress(uint memoryAddress, out int offset)
    {
        offset = 0;
        var symbol = Mapping.ToFile(memoryAddress, out var fileOffset, out _)
            ? Symbols.Data?.LookupSymbol(fileOffset)
            : null;

        if (symbol != null)
            offset = (int)(fileOffset - symbol.Address);

        return symbol;
    }

    public void Exit() => ExitRequested?.Invoke();

    public List<StackFrame> Stack
    {
        get
        {
            if (Session.IsPaused && Session.Version != _lastStackVersion)
            {
                _stack = GetStackTrace();
                _lastStackVersion = Session.Version;
            }

            return _stack;
        }
    }

    public uint? SelectedAddress { get; set; }
    public int? SelectedFrameIndex { get; set; }
    public StackFrame? SelectedFrame =>
        SelectedFrameIndex == null || SelectedFrameIndex >= Stack.Count ? null : Stack[SelectedFrameIndex.Value];

    public List<StackFrame> GetStackTrace()
    {
        var r = Session.Registers;

        var stackRegion = GetStackRegion((uint)r.ebp);
        if (stackRegion == null)
        {
            Log.Warn("No stack region found");
            return new List<StackFrame>();
        }

        uint stackBase = stackRegion.MemoryEnd;

        uint ip = (uint)r.eip;
        uint bp = (uint)r.ebp;

        var ipSymbol = LookupSymbolForAddress(ip, out var offset);
        var stack = new List<StackFrame> { new(bp) };

        if (ipSymbol != null)
            stack[0].Functions.Add(new(ipSymbol, ip, offset));

        var dwords = GetDwords(bp, stackBase);

        uint GetDword(uint addr)
        {
            if (addr < r.ebp)
                return 0;

            uint index = (addr - (uint)r.ebp) / 4;
            if (index >= dwords.Length)
                return 0;

            return dwords[index];
        }

        while (bp != 0)
        {
            bp = GetDword(bp);
            if (bp != 0)
                stack.Add(new StackFrame(bp));
        }

        for (int i = 0; i < stack.Count; i++)
        {
            var limit = i < stack.Count - 2 ? stack[i + 1].BasePointer : stackBase;
            for (uint addr = stack[i].BasePointer; addr < limit; addr += 4)
            {
                var value = GetDword(addr);
                var symbol = LookupSymbolForAddress(value, out offset);
                if (symbol?.Context is GFunction)
                    stack[i].Functions.Add(new(symbol, value, offset));
            }
        }

        return stack;
    }

    MemoryRegion? GetStackRegion(uint addressHint) =>
        Mapping.Regions.FirstOrDefault(x => x.Contains(addressHint) && x.Type == MemoryType.Stack);

    uint[] GetDwords(uint from, uint to)
    {
        if (to < from)
            throw new InvalidOperationException("Tried to get array of dwords but the supplied range was backwards");
        var byteCount = to - from;
        if (byteCount % 4 != 0)
            throw new InvalidOperationException(
                "Tried to get array of dwords but the length supplied was not a multiple of 4"
            );

        var num = byteCount / 4;
        var buf = new uint[num];
        Session.Memory.ReadIntoSpan(from, byteCount, MemoryMarshal.Cast<uint, byte>(buf));
        return buf;
    }
}
