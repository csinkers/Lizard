using GhidraProgramData;

namespace Lizard;

public interface ISymbolStore
{
    Symbol? LookupSymbol(uint memoryAddress);
}