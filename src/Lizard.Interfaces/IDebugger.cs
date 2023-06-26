namespace Lizard.Interfaces;

public interface IDebugger
{
    ITracer Log { get; }
    IMemoryCache Memory { get; } // Cached memory access
    bool AddMenuItem(IMenuItem item);
    bool RemoveMenuItem(IMenuItem item);
    bool RegisterMemoryReader(IMemoryReader memoryReader);
    bool UnregisterMemoryReader(IMemoryReader memoryReader);
    bool RegisterSymbolStore(ISymbolStore symbolStore);
    bool UnregisterSymbolStore(ISymbolStore symbolStore);
    void AddWindow(Action renderAction);
    void RemoveWindow(Action renderAction);
    // HostPrx
}