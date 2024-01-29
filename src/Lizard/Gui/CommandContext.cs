using GhidraProgramData;

namespace Lizard.Gui;

public class CommandContext
{
    public event Action? ExitRequested;

    public CommandContext(DebugSessionProvider sessionProvider, MemoryMapping mapping, SymbolStore symbols, ProjectManager projectManager)
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

    public Symbol? LookupSymbolForAddress(uint memoryAddress) =>
        Mapping.ToFile(memoryAddress) is var (fileOffset, _) 
            ? Symbols.Data?.LookupSymbol(fileOffset) 
            : null;

    public void Exit() => ExitRequested?.Invoke();
}