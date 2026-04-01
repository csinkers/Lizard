using GhidraProgramData;
using Lizard.Config;
using Lizard.Core.Unwind;
using Lizard.Memory;
using Lizard.Session;
using Lizard.Util;

namespace Lizard.Core;

public class CommandContext
{
    static readonly LogTopic Log = new("Context");
    readonly UnwindManager _unwindManager;
    List<StackFrame> _stack = [];
    int _lastStackVersion = -1;

    public event Action? ExitRequested;

    public CommandContext(
        DebugSessionProvider sessionProvider,
        MemoryMapping mapping,
        SymbolStore symbols,
        ProjectManager projectManager,
        UnwindManager unwindManager
    )
    {
        SessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
        Mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        Symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
        ProjectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _unwindManager = unwindManager ?? throw new ArgumentNullException(nameof(unwindManager));
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

    List<StackFrame> GetStackTrace()
    {
        var r = Session.Registers;

        var stackRegion = GetStackRegion(r.Ebp);
        if (stackRegion == null)
        {
            Log.Warn("No stack region found");
            return [];
        }

        uint stackBase = stackRegion.MemoryEnd;
        if (r.Ebp > stackBase)
        {
            Log.Error("Invalid stack data: current frame pointer is higher than stack base");
            return [];
        }

        var ipSymbol = LookupSymbolForAddress(r.Eip, out var offset);
        var frame = new StackFrame(r.Ebp);
        var stack = new List<StackFrame> { frame };

        if (ipSymbol != null)
            stack[0].Function = new StackFunction(ipSymbol, r.Eip, offset);

        OffsetMemory stackMemory = GetDwords(r.Ebp, stackBase);
        var context = new UnwinderContext(this, stackMemory);

        while ((frame = _unwindManager.TryUnwindFrame(context, frame)) != null)
            stack.Add(frame);

        return stack;
    }

    MemoryRegion? GetStackRegion(uint addressHint) =>
        Mapping.Regions.FirstOrDefault(x => x.Contains(addressHint) && x.Type == MemoryType.Stack);

    OffsetMemory GetDwords(uint from, uint to)
    {
        if (to < from)
            throw new InvalidOperationException("Tried to get array of dwords but the supplied range was backwards");

        var byteCount = to - from;
        if (byteCount % 4 != 0)
        {
            throw new InvalidOperationException(
                "Tried to get array of dwords but the length supplied was not a multiple of 4"
            );
        }

        var buf = new byte[byteCount];
        Session.Memory.ReadIntoSpan(from, byteCount, buf);
        return new OffsetMemory(buf, from);
    }
}
