using GhidraProgramData;

namespace Lizard.Gui.Windows.Watch;

public sealed class WatcherCore
{
    readonly ProgramDataManager _programDataManager;
    readonly IMemoryCache _memory;
    readonly ITextureStore _textures;
    DrawContext? _drawContext;
    bool _programDataDirty = true;

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

    public WatcherCore(ProgramDataManager programDataManager, IMemoryCache memory, ITextureStore textures)
    {
        _programDataManager = programDataManager ?? throw new ArgumentNullException(nameof(programDataManager));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _textures = textures ?? throw new ArgumentNullException(nameof(textures));
        programDataManager.DataLoading += () => _programDataDirty = true;

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

    // const string SymbolPath = @"C:\Depot\bb\ualbion_extra\SR-Main.exe.xml";
    public void Draw()
    {
        if (_programDataDirty)
        {
            _drawContext = _programDataManager.Data == null 
                ? null 
                : new DrawContext(_programDataManager.Data, _memory, _textures);
            _programDataDirty = false;
        }

        if (_drawContext == null)
            return;

        _drawContext.Now = DateTime.UtcNow.Ticks;
        _drawContext.Filter = Filter;

        var rootRenderer = _drawContext.Renderers.Get(_drawContext.Data.Root);
        var history = _drawContext.History.GetOrCreateHistory(Constants.RootNamespaceName, rootRenderer);

        rootRenderer.Draw(history, 0, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, _drawContext);
        _drawContext.Refreshed = false;
        _drawContext.History.CycleHistory();
    }

    public void Update()
    {
        if (_drawContext != null)
        {
            _drawContext.Memory.Dirty();
            _drawContext.Refreshed = true;
        }

        LastUpdateTimeUtc = DateTime.UtcNow;
    }
}
