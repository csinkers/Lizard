namespace Lizard.Interfaces;

public record SymbolInfo(uint Address, string Name, SymbolType SymbolType, object? Context);