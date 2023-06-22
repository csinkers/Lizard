using GhidraProgramData;
using Lizard.Interfaces;

namespace Lizard.Watch;

public class SymbolStore : ISymbolStore
{
    readonly ProgramData _data;
    public SymbolStore(ProgramData data) => _data = data ?? throw new ArgumentNullException(nameof(data));

    public int CodeOffset { get; set; }
    public int DataOffset { get; set; }
    public SymbolInfo? Lookup(uint address)
    {
        var (symAddress, name, context) = _data.Lookup(address);
        var symbolType = context switch
        {
            GFunction _ => SymbolType.Function,
            GGlobal _ => SymbolType.Global,
            _ => SymbolType.Unknown
        };
        return new(symAddress, name, symbolType, context);
    }
}