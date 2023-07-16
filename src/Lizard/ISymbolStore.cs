namespace Lizard;

public interface ISymbolStore
{
    int Offset { get; }
    SymbolInfo? Lookup(uint address);
}