namespace Lizard.Interfaces;

public interface ISymbolStore
{
    int CodeOffset { get; set; }
    int DataOffset { get; set; }
    SymbolInfo? Lookup(uint address);
}