using Lizard.Memory;

namespace Lizard.Gui.Windows.Watch;

public class HistoryCache : IHistoryCreationContext
{
    static readonly TimeSpan CycleInterval = TimeSpan.FromSeconds(5);
    Dictionary<string, History> _oldHistory = new();
    Dictionary<string, History> _history = new();
    DateTime _lastCycleTime;
    readonly RendererCache _renderers;
    readonly MemoryMapping _mapping;

    public HistoryCache(RendererCache renderers, MemoryMapping mapping)
    {
        _renderers = renderers ?? throw new ArgumentNullException(nameof(renderers));
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
    }

    public History? TryGetHistory(string path)
    {
        if (_history.TryGetValue(path, out var history)) // Was used recently
            return history;

        if (!_oldHistory.TryGetValue(path, out history))
            return null;

        _history[path] = history; // Wasn't used in the current cache, so put it in
        return history;
    }

    public uint ToMemoryAddress(uint fileAddress) =>
        _mapping.ToMemory(fileAddress, out var memOffset, out _) ? memOffset : 0;

    public uint ToFileAddress(uint memoryAddress) =>
        _mapping.ToFile(memoryAddress, out var fileOffset, out _) ? fileOffset : 0;

    string? IHistoryCreationContext.ResolvePath(string path, string context)
    {
        var span = path.AsSpan();
        var resultSpan = context.AsSpan();

        if (span[0] == '/')
        {
            span = span[1..];
            resultSpan = "";
        }

        while (span.StartsWith("../"))
        {
            int index = resultSpan.LastIndexOf('/');
            if (index == -1)
                return null;

            resultSpan = resultSpan[..index];
            span = span[3..];
        }

        if (span.Length == 0)
            return null;

        var ancestorPath = resultSpan.ToString();
        var ancestor = TryGetHistory(ancestorPath);
        return ancestor?.Type.BuildPath(ancestorPath, span.ToString());
    }

    RendererCache IHistoryCreationContext.Renderers => _renderers;

    public History CreateHistory(string path, IGhidraRenderer renderer)
    {
        var history = renderer.HistoryConstructor(path, this); // Wasn't used in the current or the previous cache
        _history[path] = history; // Wasn't used in the current cache, so put it in
        return history;
    }

    public History GetOrCreateHistory(string path, IGhidraRenderer renderer) =>
        TryGetHistory(path) ?? CreateHistory(path, renderer);

    public void CycleHistory()
    {
        if (DateTime.UtcNow - _lastCycleTime <= CycleInterval)
            return;

        _oldHistory = _history;
        _history = new Dictionary<string, History>();
        _lastCycleTime = DateTime.UtcNow;
    }
}
