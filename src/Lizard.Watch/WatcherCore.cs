using GhidraProgramData;
using ImGuiNET;
using Lizard.Interfaces;

namespace Lizard.Watch;

public sealed class WatcherCore : IDisposable
{
    readonly IMemoryReader _reader;
    readonly ITextureStore _textures;
    DrawContext? _drawContext;

    public string Filter { get; set; } = "";
    // public Config Config { get; }
    public DateTime LastUpdateTimeUtc { get; private set; } = DateTime.MinValue;

    // Active set
    // Show available symbols + active symbols
    // Allow expanding arrays, slicing across single struct elem etc
    // Update mem data using background thread
    // Track value history?
    // Highlight changed values
    // Searching / filtering.

    public WatcherCore(IMemoryReader reader, ITextureStore textures)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _textures = textures ?? throw new ArgumentNullException(nameof(textures));
        // Config = Config.Load();

        /* foreach (var name in config.Watches)
        {
            var index = name.IndexOf('/');
            var nsPart = index == -1 ? "" : name[..index];
            var watchPart = index == -1 ? name : name[(index + 1)..];
            var ns = _namespaces.FirstOrDefault(x => x.Name == nsPart);
            var watch = ns?.Watches.FirstOrDefault(x => x.Name == watchPart);
            if (watch != null)
                watch.IsActive = true;
        }*/
    }

    const string SymbolPath = @"C:\Depot\bb\ualbion_extra\SR-Main.exe.xml";
    public void Draw()
    {
        if (ImGui.Button("Reload from XML"))
            _drawContext = new DrawContext(SymbolPath, _reader, _textures);

        if (_drawContext == null)
            return;

        _drawContext.Now = DateTime.UtcNow.Ticks;
        _drawContext.Filter = Filter;

        var rootRenderer = _drawContext.Renderers.Get(_drawContext.Data.Root);
        var history = _drawContext.History.GetOrCreateHistory(Constants.RootNamespaceName, rootRenderer);

        rootRenderer.Draw(history, 0, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, _drawContext);
        _drawContext.Refreshed = false;
    }

    public void Update()
    {
        if (_drawContext != null)
        {
            _drawContext.History.CycleHistory();
            _drawContext.Memory.Refresh();
            _drawContext.Refreshed = true;
        }

        LastUpdateTimeUtc = DateTime.UtcNow;
    }

    public void Dispose() => _reader.Dispose();
}